using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Results.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Common.Mapping;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Results.Queries.GetSeasonResults;

public interface IGetSeasonResultsQueryHandler
{
    Task<Result<SeasonResultsDto>> ExecuteAsync(
        GetSeasonResultsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetSeasonResultsQueryHandler : IGetSeasonResultsQueryHandler
{
    private readonly ILogger<GetSeasonResultsQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IContestClientFactory _contestClientFactory;

    public GetSeasonResultsQueryHandler(
        ILogger<GetSeasonResultsQueryHandler> logger,
        AppDataContext dbContext,
        IContestClientFactory contestClientFactory)
    {
        _logger = logger;
        _dbContext = dbContext;
        _contestClientFactory = contestClientFactory;
    }

    public async Task<Result<SeasonResultsDto>> ExecuteAsync(
        GetSeasonResultsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "GetSeasonResultsQueryHandler.ExecuteAsync sport={Sport} league={League} season={Season}",
            query.Sport, query.League, query.SeasonYear);

        // Resolve sport+league to the canonical Sport enum so we can pick
        // the right sport-keyed Producer client.
        Sport mode;
        try
        {
            mode = ModeMapper.ResolveMode(query.Sport, query.League);
        }
        catch (NotSupportedException)
        {
            return new Failure<SeasonResultsDto>(
                default!,
                ResultStatus.BadRequest,
                [new ValidationFailure("sport", $"Unsupported sport/league combination: {query.Sport}/{query.League}")]);
        }

        // Coarse date window for the season — NCAA FB starts late Aug,
        // bowls finish early Jan of the following year. NFL is similar.
        // The window is intentionally generous; Producer's sport-scoped
        // filter is the real fence.
        var seasonStart = new DateTime(query.SeasonYear, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var seasonEnd = new DateTime(query.SeasonYear + 1, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Pull approved previews in the date window. Latest approved per
        // contest wins (mirrors the GetLeagueWeekMatchups handler).
        var previews = await _dbContext.MatchupPreviews
            .AsNoTracking()
            .Where(p => string.IsNullOrEmpty(p.ValidationErrors)
                     && p.RejectedUtc == null
                     && p.CreatedUtc >= seasonStart
                     && p.CreatedUtc < seasonEnd)
            .OrderByDescending(p => p.CreatedUtc)
            .Select(p => new
            {
                p.ContestId,
                p.PredictedStraightUpWinner,
                p.PredictedSpreadWinner,
                p.CreatedUtc
            })
            .ToListAsync(cancellationToken);

        var latestPerContest = previews
            .GroupBy(p => p.ContestId)
            .ToDictionary(g => g.Key, g => g.First()); // already ordered desc

        if (latestPerContest.Count == 0)
        {
            _logger.LogInformation(
                "No approved previews in window for {Sport}/{League} season {Season}",
                query.Sport, query.League, query.SeasonYear);

            return new Success<SeasonResultsDto>(new SeasonResultsDto
            {
                Sport = query.Sport,
                League = query.League,
                SeasonYear = query.SeasonYear
            });
        }

        var contestIds = latestPerContest.Keys.ToList();

        // Producer is sport-scoped — non-NCAA contests drop here naturally.
        var matchupsResult = await _contestClientFactory
            .Resolve(mode)
            .GetMatchupsByContestIds(contestIds, MarkDirection.Roundel, cancellationToken);

        if (!matchupsResult.IsSuccess)
        {
            _logger.LogError(
                "ContestClient failed for {Sport}/{League} season {Season}",
                query.Sport, query.League, query.SeasonYear);

            return new Failure<SeasonResultsDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("matchups", "Failed to retrieve matchup data from Producer")]);
        }

        var matchups = matchupsResult.Value ?? [];

        // Build tiles. Computed hit fields are nullable to encode
        // "cannot evaluate" (missing pick or missing actual) distinctly
        // from "miss".
        var tiles = new List<(DateTime SeasonWeekEndDate, GameResultTileDto Tile)>();

        foreach (var m in matchups)
        {
            if (!latestPerContest.TryGetValue(m.ContestId, out var preview))
                continue; // shouldn't happen — we filtered IDs from previews

            var tile = new GameResultTileDto
            {
                ContestId = m.ContestId,
                StartDateUtc = m.StartDateUtc,

                Away = m.Away,
                AwayShort = m.AwayShort,
                AwayLogoUri = m.AwayLogoUri,
                AwayFranchiseSeasonId = m.AwayFranchiseSeasonId,
                AwayScore = m.AwayScore,

                Home = m.Home,
                HomeShort = m.HomeShort,
                HomeLogoUri = m.HomeLogoUri,
                HomeFranchiseSeasonId = m.HomeFranchiseSeasonId,
                HomeScore = m.HomeScore,

                Spread = m.SpreadCurrent,

                PredictedStraightUpWinner = preview.PredictedStraightUpWinner,
                PredictedSpreadWinner = preview.PredictedSpreadWinner,

                ActualStraightUpWinner = m.WinnerFranchiseSeasonId,
                ActualSpreadWinner = m.SpreadWinnerFranchiseSeasonId
            };

            // SU evaluation
            if (preview.PredictedStraightUpWinner.HasValue && m.WinnerFranchiseSeasonId.HasValue)
            {
                tile.SuHit = preview.PredictedStraightUpWinner.Value == m.WinnerFranchiseSeasonId.Value;
            }

            // ATS evaluation. Spread winner null on the canonical row means
            // either the line was a push or the line wasn't captured — treat
            // both as "cannot evaluate" unless we have evidence of a push.
            if (preview.PredictedSpreadWinner.HasValue && m.SpreadWinnerFranchiseSeasonId.HasValue)
            {
                tile.AtsHit = preview.PredictedSpreadWinner.Value == m.SpreadWinnerFranchiseSeasonId.Value;
            }

            tiles.Add((m.SeasonWeekEndDate, tile));
        }

        // Group by SeasonWeekEndDate, order chronologically, label "Week N".
        // Avoids depending on the producer-side WeekNumber resolution for MVP.
        var weeks = tiles
            .GroupBy(t => t.SeasonWeekEndDate.Date)
            .OrderBy(g => g.Key)
            .Select((g, idx) =>
            {
                var games = g.Select(x => x.Tile)
                    .OrderBy(t => t.StartDateUtc)
                    .ToList();

                return new WeekResultsDto
                {
                    WeekNumber = idx + 1,
                    SeasonWeekEndDate = g.Key,
                    Games = games,
                    Aggregate = AggregateOf(games)
                };
            })
            .ToList();

        var seasonAggregate = AggregateOf(weeks.SelectMany(w => w.Games));

        var result = new SeasonResultsDto
        {
            Sport = query.Sport,
            League = query.League,
            SeasonYear = query.SeasonYear,
            Aggregate = seasonAggregate,
            Weeks = weeks
        };

        _logger.LogInformation(
            "GetSeasonResults returning {WeekCount} weeks, {GameCount} games — SU {SuW}-{SuL}, ATS {AtsW}-{AtsL}-{AtsP}",
            result.Weeks.Count,
            seasonAggregate.TotalGames,
            seasonAggregate.SuWins, seasonAggregate.SuLosses,
            seasonAggregate.AtsWins, seasonAggregate.AtsLosses, seasonAggregate.AtsPushes);

        return new Success<SeasonResultsDto>(result);
    }

    private static AggregateRecordDto AggregateOf(IEnumerable<GameResultTileDto> games)
    {
        var list = games as IList<GameResultTileDto> ?? games.ToList();
        return new AggregateRecordDto
        {
            SuWins = list.Count(g => g.SuHit == true),
            SuLosses = list.Count(g => g.SuHit == false),
            AtsWins = list.Count(g => g.AtsHit == true),
            AtsLosses = list.Count(g => g.AtsHit == false),
            AtsPushes = list.Count(g => g.AtsPush),
            TotalGames = list.Count
        };
    }
}
