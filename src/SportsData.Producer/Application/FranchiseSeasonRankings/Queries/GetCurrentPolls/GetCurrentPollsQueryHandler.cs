using Dapper;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.FranchiseSeasonRankings.Queries.GetCurrentPolls;

public interface IGetCurrentPollsQueryHandler
{
    Task<Result<List<FranchiseSeasonPollDto>>> ExecuteAsync(
        GetCurrentPollsQuery query,
        CancellationToken cancellationToken = default);
}

public class GetCurrentPollsQueryHandler : IGetCurrentPollsQueryHandler
{
    private readonly TeamSportDataContext _dataContext;
    private readonly ILogger<GetCurrentPollsQueryHandler> _logger;
    private readonly ILogoSelectionService _logoSelectionService;
    private readonly ProducerSqlQueryProvider _sqlProvider;

    public GetCurrentPollsQueryHandler(
        TeamSportDataContext dataContext,
        ILogger<GetCurrentPollsQueryHandler> logger,
        ILogoSelectionService logoSelectionService,
        ProducerSqlQueryProvider sqlProvider)
    {
        _dataContext = dataContext;
        _logger = logger;
        _logoSelectionService = logoSelectionService;
        _sqlProvider = sqlProvider;
    }

    public async Task<Result<List<FranchiseSeasonPollDto>>> ExecuteAsync(
        GetCurrentPollsQuery query,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("GetCurrentPolls started. SeasonYear={SeasonYear}", query.SeasonYear);

        try
        {
            var pollsToLoad = new List<string> { "cfp", "ap", "usa" };
            var allEntries = new Dictionary<string, List<PollEntryRow>>();
            var connection = _dataContext.Database.GetDbConnection();
            var sql = _sqlProvider.GetPollByTypeAndSeason();

            // One SQL call per poll — each is a single CTE query, no round-trips
            foreach (var pollId in pollsToLoad)
            {
                var entries = (await connection.QueryAsync<PollEntryRow>(
                    new CommandDefinition(sql, new { SeasonYear = query.SeasonYear, PollId = pollId },
                        cancellationToken: cancellationToken))).ToList();

                if (entries.Count > 0)
                    allEntries[pollId] = entries;
            }

            if (allEntries.Count == 0)
            {
                _logger.LogWarning("No polls found. SeasonYear={SeasonYear}", query.SeasonYear);
                return new Failure<List<FranchiseSeasonPollDto>>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("seasonYear", $"No polls found for season year {query.SeasonYear}")]);
            }

            // Single batch logo load for all franchises across all polls
            var allFranchiseIds = allEntries.Values
                .SelectMany(e => e.Select(x => x.FranchiseId))
                .Distinct().ToList();
            var allFranchiseSeasonIds = allEntries.Values
                .SelectMany(e => e.Select(x => x.FranchiseSeasonId))
                .Distinct().ToList();

            var franchiseLogos = await _dataContext.FranchiseLogos
                .AsNoTracking()
                .Where(fl => allFranchiseIds.Contains(fl.FranchiseId))
                .ToListAsync(cancellationToken);

            var seasonLogos = await _dataContext.FranchiseSeasonLogos
                .AsNoTracking()
                .Where(fsl => allFranchiseSeasonIds.Contains(fsl.FranchiseSeasonId))
                .ToListAsync(cancellationToken);

            var franchiseLogoLookup = franchiseLogos
                .GroupBy(l => l.FranchiseId)
                .ToDictionary(g => g.Key, g => (IEnumerable<ILogo>)g.ToList());
            var seasonLogoLookup = seasonLogos
                .GroupBy(l => l.FranchiseSeasonId)
                .ToDictionary(g => g.Key, g => (IEnumerable<ILogo>)g.ToList());

            // Build poll DTOs
            var polls = new List<FranchiseSeasonPollDto>();

            foreach (var (pollId, entries) in allEntries)
            {
                var first = entries[0];

                var pollEntries = entries.Select(x =>
                {
                    franchiseLogoLookup.TryGetValue(x.FranchiseId, out var fLogos);
                    seasonLogoLookup.TryGetValue(x.FranchiseSeasonId, out var fsLogos);

                    var logoUriDark = _logoSelectionService.SelectWithFallback(fsLogos, fLogos, darkBackground: true);
                    var logoUriLight = _logoSelectionService.SelectWithFallback(fsLogos, fLogos, darkBackground: false);

                    return new FranchiseSeasonPollDto.FranchiseSeasonPollEntryDto
                    {
                        FranchiseLogoUrl = logoUriDark?.OriginalString ?? string.Empty,
                        FranchiseLogoUrlDark = logoUriDark?.OriginalString,
                        FranchiseLogoUrlLight = logoUriLight?.OriginalString,
                        FranchiseName = x.FranchiseName,
                        FranchiseSlug = x.FranchiseSlug,
                        Rank = x.Rank,
                        FirstPlaceVotes = x.FirstPlaceVotes,
                        FranchiseSeasonId = x.FranchiseSeasonId,
                        Points = (int)x.Points,
                        Trend = x.Trend,
                        Losses = x.Losses,
                        PreviousRank = x.PreviousRank,
                        Wins = x.Wins
                    };
                }).ToList();

                polls.Add(new FranchiseSeasonPollDto
                {
                    Entries = pollEntries,
                    PollId = pollId,
                    PollName = first.PollName,
                    SeasonYear = query.SeasonYear,
                    Week = first.WeekNumber,
                    HasFirstPlaceVotes = pollEntries.Sum(x => x.FirstPlaceVotes) > 0,
                    HasPoints = pollEntries.Sum(x => x.Points) > 0,
                    HasTrends = pollEntries.Any(x => x.Trend != null),
                    PollDateUtc = first.PollDateUtc
                });

                _logger.LogInformation(
                    "Poll loaded. PollId={PollId}, EntryCount={EntryCount}, Week={Week}",
                    pollId, pollEntries.Count, first.WeekNumber);
            }

            _logger.LogInformation(
                "GetCurrentPolls completed. SeasonYear={SeasonYear}, PollCount={PollCount}",
                query.SeasonYear, polls.Count);

            return new Success<List<FranchiseSeasonPollDto>>(polls);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetCurrentPolls. SeasonYear={SeasonYear}", query.SeasonYear);
            return new Failure<List<FranchiseSeasonPollDto>>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("Error", ex.Message)]);
        }
    }

    private record PollEntryRow
    {
        public int WeekNumber { get; init; }
        public DateTime PollDateUtc { get; init; }
        public string PollName { get; init; } = string.Empty;
        public Guid FranchiseSeasonId { get; init; }
        public Guid FranchiseId { get; init; }
        public string FranchiseSlug { get; init; } = string.Empty;
        public string FranchiseName { get; init; } = string.Empty;
        public int Wins { get; init; }
        public int Losses { get; init; }
        public int Rank { get; init; }
        public int? PreviousRank { get; init; }
        public double Points { get; init; }
        public int? FirstPlaceVotes { get; init; }
        public string? Trend { get; init; }
    }
}
