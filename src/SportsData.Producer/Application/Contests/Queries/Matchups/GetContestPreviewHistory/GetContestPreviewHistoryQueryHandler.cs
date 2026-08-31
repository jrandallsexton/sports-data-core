using Dapper;

using FluentValidation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Producer.Application.FranchiseSeasons.Queries.GetFranchiseSeasonMetricsById;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Sql;

namespace SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

public interface IGetContestPreviewHistoryQueryHandler
{
    Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestPreviewHistoryQuery query,
        CancellationToken cancellationToken = default);
}

public class GetContestPreviewHistoryQueryHandler : IGetContestPreviewHistoryQueryHandler
{
    private readonly TeamSportDataContext _dbContext;
    private readonly ProducerSqlQueryProvider _sqlProvider;
    private readonly IValidator<GetContestPreviewHistoryQuery> _validator;
    private readonly IGetFranchiseSeasonMetricsByIdQueryHandler _metricsHandler;

    private readonly IDistributedCache _cache;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <summary>
    /// TTL once the contest's inputs have frozen — see <see cref="ResolveCacheTtl"/>.
    /// </summary>
    private static readonly TimeSpan SettledTtl = TimeSpan.FromDays(7);

    /// <summary>
    /// TTL while the contest is still ahead of us. Short because the spread moves.
    /// </summary>
    private static readonly TimeSpan LiveTtl = TimeSpan.FromHours(1);

    /// <summary>
    /// TTL for a contest id we could not resolve. Kept brief so a request that
    /// arrives before the contest is sourced does not pin an empty result.
    /// </summary>
    private static readonly TimeSpan UnresolvedContestTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Inputs freeze this long after kickoff, at which point the entry can live a
    /// long time.
    /// </summary>
    private static readonly TimeSpan SettlesAfterStart = TimeSpan.FromHours(24);

    public GetContestPreviewHistoryQueryHandler(
        TeamSportDataContext dbContext,
        ProducerSqlQueryProvider sqlProvider,
        IValidator<GetContestPreviewHistoryQuery> validator,
        IGetFranchiseSeasonMetricsByIdQueryHandler metricsHandler,
        IDistributedCache cache,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _sqlProvider = sqlProvider;
        _validator = validator;
        _metricsHandler = metricsHandler;
        _cache = cache;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Cache key for one preview-history result.
    /// </summary>
    /// <remarks>
    /// Includes MeetingCount and RecentGameCount deliberately. The query exposes
    /// both, so two callers asking for different depths must not share an entry.
    /// Every caller happens to use the 5/5 defaults today, which is precisely why
    /// keying on contest id alone would have looked correct indefinitely.
    /// <para>
    /// The v1 segment is a payload-shape version: changing ContestPreviewHistoryDto
    /// invalidates every entry by bumping it, rather than serving old shapes to new
    /// deserializers until the TTL expires.
    /// </para>
    /// </remarks>
    private static string BuildCacheKey(GetContestPreviewHistoryQuery query) =>
        $"preview-history:v1:{query.ContestId}:{query.MeetingCount}:{query.RecentGameCount}";

    /// <summary>
    /// How long this result stays valid.
    /// </summary>
    /// <remarks>
    /// Almost everything here is genuinely historical — head-to-head meetings and
    /// prior-season records cannot change. The exception is the spread:
    /// <see cref="BuildSpreadContextAsync"/> reads the contest's current line and
    /// derives favorite, magnitude and ATS bucket from it, and lines move through
    /// the week. A multi-day entry for an upcoming game would therefore serve a
    /// stale line dressed up as historical fact.
    /// <para>
    /// So: short TTL until the game is 24h past kickoff, long TTL after. An hour
    /// still collapses thousands of user views into one computation, which is
    /// where nearly all the benefit lives.
    /// </para>
    /// </remarks>
    private TimeSpan ResolveCacheTtl(DateTime? startDateUtc)
    {
        if (startDateUtc is null)
            return UnresolvedContestTtl;

        var settlesAt = startDateUtc.Value.Add(SettlesAfterStart);

        return _dateTimeProvider.UtcNow() >= settlesAt
            ? SettledTtl
            : LiveTtl;
    }

    /// <summary>
    /// Dapper row for the prior-season query: a PreviewGameResultDto plus
    /// which TARGET team (Away/Home) the row belongs to.
    /// </summary>
    private class PriorSeasonRow : PreviewGameResultDto
    {
        public string Side { get; set; } = default!;
    }

    public async Task<Result<ContestPreviewHistoryDto>> ExecuteAsync(
        GetContestPreviewHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await _validator.ValidateAsync(query, cancellationToken);
        if (!validationResult.IsValid)
        {
            return new Failure<ContestPreviewHistoryDto>(
                default!,
                ResultStatus.Validation,
                validationResult.Errors);
        }

        // Assembling this DTO costs ~10 sequential round trips (head-to-head,
        // prior-season summaries for both sides, spread target, two margin facts,
        // two ATS buckets), and it is identical for every user viewing the same
        // matchup — it is not user-scoped. Cache read sits after validation so a
        // malformed query still fails fast.
        var cacheKey = BuildCacheKey(query);

        var fromCache = await _cache.GetRecordAsync<ContestPreviewHistoryDto>(cacheKey);

        if (fromCache is not null)
            return new Success<ContestPreviewHistoryDto>(fromCache);

        var connection = _dbContext.Database.GetDbConnection();

        var headToHead = (await connection.QueryAsync<PreviewGameResultDto>(
            new CommandDefinition(
                _sqlProvider.GetContestHeadToHeadResults(),
                new { query.ContestId, Count = query.MeetingCount },
                cancellationToken: cancellationToken))).ToList();

        var priorSeasonRows = (await connection.QueryAsync<PriorSeasonRow>(
            new CommandDefinition(
                _sqlProvider.GetContestPriorSeasonResults(),
                new { query.ContestId, Count = query.RecentGameCount },
                cancellationToken: cancellationToken))).ToList();

        // An unknown contest yields empty lists everywhere (the target CTE
        // matches nothing) — an empty history is a normal state for a
        // first-ever meeting, so no NotFound here; the caller degrades
        // gracefully either way.
        var dto = new ContestPreviewHistoryDto
        {
            HeadToHead = headToHead,
            AwayPriorSeasonGames = priorSeasonRows
                .Where(x => x.Side == "Away").Cast<PreviewGameResultDto>().ToList(),
            HomePriorSeasonGames = priorSeasonRows
                .Where(x => x.Side == "Home").Cast<PreviewGameResultDto>().ToList()
        };

        var target = await _dbContext.Contests
            .AsNoTracking()
            .Where(c => c.Id == query.ContestId)
            .Select(c => new
            {
                c.SeasonYear,
                c.StartDateUtc,
                c.AwayTeamFranchiseSeasonId,
                c.HomeTeamFranchiseSeasonId
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (target is not null)
        {
            dto.AwayPriorSeason = await BuildPriorSeasonSummaryAsync(
                target.AwayTeamFranchiseSeasonId, target.SeasonYear, cancellationToken);
            dto.HomePriorSeason = await BuildPriorSeasonSummaryAsync(
                target.HomeTeamFranchiseSeasonId, target.SeasonYear, cancellationToken);
        }

        dto.SpreadContext = await BuildSpreadContextAsync(connection, query.ContestId, cancellationToken);

        await _cache.SetRecordAsync(cacheKey, dto, ResolveCacheTtl(target?.StartDateUtc));

        return new Success<ContestPreviewHistoryDto>(dto);
    }

    // ─── Spread-conditioned facts ───────────────────────────────────────────

    /// <summary>Spread-value data exists from this season on (odds-era floor).</summary>
    private const int MarketDataFloorSeason = 2022;

    /// <summary>
    /// ATS facts bucket on football key numbers rather than the exact line:
    /// "as a 35+ favorite" reads naturally and accrues a meaningful sample,
    /// where "as a 38.5-point favorite" would almost always be n=0.
    /// </summary>
    private static readonly double[] AtsKeyNumbers = [3, 7, 10, 14, 21, 28, 35];

    private class SpreadTargetRow
    {
        public DateTime StartDateUtc { get; set; }
        public int SeasonYear { get; set; }
        public Guid AwayFranchiseId { get; set; }
        public Guid HomeFranchiseId { get; set; }
        public string AwayTeam { get; set; } = default!;
        public string HomeTeam { get; set; } = default!;
        public double? HomeSpread { get; set; }
        public string? SpreadDetails { get; set; }
    }

    private class MarginFactRow
    {
        public DateTime? GameDate { get; set; }
        public int? SeasonYear { get; set; }
        public string? Phase { get; set; }
        public string? Note { get; set; }
        public string? HomeTeam { get; set; }
        public string? AwayTeam { get; set; }
        public int? HomeScore { get; set; }
        public int? AwayScore { get; set; }
        public string? Winner { get; set; }
        public string? SpreadWinner { get; set; }
        public Guid? OpponentFranchiseSeasonId { get; set; }
        public int CountLastFiveSeasons { get; set; }
        public int? SearchFloorSeason { get; set; }
    }

    private class AtsBucketRow
    {
        public int Games { get; set; }
        public int Covers { get; set; }
    }

    /// <summary>
    /// Facts conditioned on the target contest's live line: when each side
    /// last won/lost by the spread's magnitude (score tier, full corpus,
    /// with the qualifying opponent's records as quality context) and each
    /// side's ATS record at the nearest key-number bucket (market tier,
    /// odds era only). Null when the contest has no line — the block is
    /// spread-derived and meaningless without one.
    /// </summary>
    private async Task<PreviewSpreadContextDto?> BuildSpreadContextAsync(
        System.Data.Common.DbConnection connection,
        Guid contestId,
        CancellationToken cancellationToken)
    {
        var target = await connection.QueryFirstOrDefaultAsync<SpreadTargetRow>(
            new CommandDefinition(
                _sqlProvider.GetContestSpreadTarget(),
                new { ContestId = contestId },
                cancellationToken: cancellationToken));

        if (target?.HomeSpread is null || target.HomeSpread.Value == 0)
            return null;

        var homeFavored = target.HomeSpread.Value < 0;
        var magnitude = Math.Abs(target.HomeSpread.Value);
        var favoriteFranchiseId = homeFavored ? target.HomeFranchiseId : target.AwayFranchiseId;
        var underdogFranchiseId = homeFavored ? target.AwayFranchiseId : target.HomeFranchiseId;

        var context = new PreviewSpreadContextDto
        {
            FavoriteTeam = homeFavored ? target.HomeTeam : target.AwayTeam,
            UnderdogTeam = homeFavored ? target.AwayTeam : target.HomeTeam,
            Magnitude = magnitude,
            SpreadDetails = target.SpreadDetails,
            FavoriteWonByMargin = await BuildMarginFactAsync(
                connection, favoriteFranchiseId, magnitude, target.StartDateUtc,
                target.SeasonYear, won: true, cancellationToken),
            UnderdogLostByMargin = await BuildMarginFactAsync(
                connection, underdogFranchiseId, magnitude, target.StartDateUtc,
                target.SeasonYear, won: false, cancellationToken)
        };

        var threshold = AtsKeyNumbers.Where(k => k <= magnitude).DefaultIfEmpty(0).Max();
        if (threshold > 0)
        {
            context.FavoriteAtsAsBigFavorite = await BuildAtsBucketFactAsync(
                connection, favoriteFranchiseId, threshold, target.StartDateUtc, asFavorite: true, cancellationToken);
            context.UnderdogAtsAsBigUnderdog = await BuildAtsBucketFactAsync(
                connection, underdogFranchiseId, threshold, target.StartDateUtc, asFavorite: false, cancellationToken);
        }

        return context;
    }

    private async Task<PreviewMarginFactDto> BuildMarginFactAsync(
        System.Data.Common.DbConnection connection,
        Guid franchiseId,
        double margin,
        DateTime asOf,
        int targetSeasonYear,
        bool won,
        CancellationToken cancellationToken)
    {
        var row = await connection.QueryFirstOrDefaultAsync<MarginFactRow>(
            new CommandDefinition(
                _sqlProvider.GetFranchiseMarginFact(),
                new
                {
                    FranchiseId = franchiseId,
                    Margin = margin,
                    AsOf = asOf,
                    // "Last 5 seasons" = the target's (partial, as-of-capped)
                    // season plus the four before it — exactly five season
                    // labels. A blowout three weeks ago belongs in the count.
                    WindowStartSeason = targetSeasonYear - 4,
                    Won = won
                },
                cancellationToken: cancellationToken));

        var fact = new PreviewMarginFactDto
        {
            CountLastFiveSeasons = row?.CountLastFiveSeasons ?? 0,
            SearchFloorSeason = row?.SearchFloorSeason ?? targetSeasonYear
        };

        if (row?.GameDate is null)
            return fact; // never happened within the corpus — the headline case

        fact.LastGame = new PreviewGameResultDto
        {
            GameDate = row.GameDate.Value,
            SeasonYear = row.SeasonYear ?? 0,
            Phase = row.Phase,
            Note = row.Note,
            HomeTeam = row.HomeTeam!,
            AwayTeam = row.AwayTeam!,
            HomeScore = row.HomeScore,
            AwayScore = row.AwayScore,
            Winner = row.Winner,
            SpreadWinner = row.SpreadWinner
        };

        // Opponent quality is what turns the fact into an argument: a
        // 40-point win over a 2-10 doormat and one over a bowl team are
        // different evidence. Records come from FranchiseSeasonRecord —
        // absent stays null (never a fabricated 0-0).
        if (row.OpponentFranchiseSeasonId is not null)
        {
            fact.OpponentSeasonRecord = await GetOverallRecordAsync(
                row.OpponentFranchiseSeasonId.Value, cancellationToken);
            fact.OpponentPriorSeasonRecord = await GetPriorSeasonOverallRecordAsync(
                row.OpponentFranchiseSeasonId.Value, cancellationToken);
        }

        return fact;
    }

    private async Task<PreviewAtsBucketFactDto> BuildAtsBucketFactAsync(
        System.Data.Common.DbConnection connection,
        Guid franchiseId,
        double threshold,
        DateTime asOf,
        bool asFavorite,
        CancellationToken cancellationToken)
    {
        var row = await connection.QueryFirstOrDefaultAsync<AtsBucketRow>(
            new CommandDefinition(
                _sqlProvider.GetFranchiseAtsBucket(),
                new
                {
                    FranchiseId = franchiseId,
                    Threshold = threshold,
                    AsOf = asOf,
                    AsFavorite = asFavorite
                },
                cancellationToken: cancellationToken));

        return new PreviewAtsBucketFactDto
        {
            Threshold = threshold,
            Games = row?.Games ?? 0,
            Covers = row?.Covers ?? 0,
            DataFloorSeason = MarketDataFloorSeason
        };
    }

    /// <summary>Overall W-L string ("3-9") for one FranchiseSeason; null when unsourced.</summary>
    private async Task<string?> GetOverallRecordAsync(
        Guid franchiseSeasonId,
        CancellationToken cancellationToken)
    {
        var stats = await _dbContext.FranchiseSeasonRecords
            .AsNoTracking()
            .Where(r => r.FranchiseSeasonId == franchiseSeasonId && r.Type == RecordTypeTotal)
            .Select(r => r.Stats
                .Where(st => st.Name == StatWins || st.Name == StatLosses)
                .Select(st => new { st.Name, st.Value })
                .ToList())
            .FirstOrDefaultAsync(cancellationToken);

        if (stats is null)
            return null;

        // Both stats or nothing: defaulting a missing side to 0 fabricates a
        // record ("0-12"), and absent-stays-null is this feature's honesty rule.
        var wins = stats.FirstOrDefault(st => st.Name == StatWins)?.Value;
        var losses = stats.FirstOrDefault(st => st.Name == StatLosses)?.Value;
        if (wins is null || losses is null)
            return null;

        return $"{(int)wins}-{(int)losses}";
    }

    /// <summary>Overall W-L string for the season BEFORE the given FranchiseSeason (cross-season identity via Franchise).</summary>
    private async Task<string?> GetPriorSeasonOverallRecordAsync(
        Guid franchiseSeasonId,
        CancellationToken cancellationToken)
    {
        var priorSeasonId = await _dbContext.FranchiseSeasons
            .AsNoTracking()
            .Where(current => current.Id == franchiseSeasonId)
            .SelectMany(current => _dbContext.FranchiseSeasons
                .Where(prior => prior.FranchiseId == current.FranchiseId
                             && prior.SeasonYear == current.SeasonYear - 1))
            .Select(prior => (Guid?)prior.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (priorSeasonId is null)
            return null;

        return await GetOverallRecordAsync(priorSeasonId.Value, cancellationToken);
    }

    // FranchiseSeasonRecord.Type values (ESPN vocabulary): the season
    // total and the conference split. Home/road/vsdiv also exist and are
    // available for future payload enrichment.
    private const string RecordTypeTotal = "total";
    private const string RecordTypeConference = "vsconf";

    private const string StatWins = "wins";
    private const string StatLosses = "losses";

    /// <summary>
    /// Prior-season summary for one team: resolve the franchise's
    /// SeasonYear-1 FranchiseSeason (cross-season identity via Franchise),
    /// read its final record from FranchiseSeasonRecord (the sourced,
    /// per-type record table — NOT the abandoned denormalized W/L columns
    /// on FranchiseSeason, which are unpopulated for NFL and inconsistent
    /// for NCAAFB), and attach that season's metrics when they exist.
    ///
    /// Null when the franchise has no prior season row OR no overall
    /// record was sourced for it — an absent block is honest; a
    /// fabricated 0-0 record is a lie the preview model will narrate.
    /// Conference W/L are null (omitted from the payload) when only the
    /// conference split is missing.
    /// </summary>
    private async Task<PreviewPriorSeasonSummaryDto?> BuildPriorSeasonSummaryAsync(
        Guid currentFranchiseSeasonId,
        int targetSeasonYear,
        CancellationToken cancellationToken)
    {
        var priorSeason = await _dbContext.FranchiseSeasons
            .AsNoTracking()
            .Where(current => current.Id == currentFranchiseSeasonId)
            .SelectMany(current => _dbContext.FranchiseSeasons
                .Where(prior => prior.FranchiseId == current.FranchiseId
                             && prior.SeasonYear == targetSeasonYear - 1))
            .Select(prior => new { prior.Id, prior.SeasonYear })
            .FirstOrDefaultAsync(cancellationToken);

        if (priorSeason is null)
            return null;

        var records = await _dbContext.FranchiseSeasonRecords
            .AsNoTracking()
            .Where(r => r.FranchiseSeasonId == priorSeason.Id
                     && (r.Type == RecordTypeTotal || r.Type == RecordTypeConference))
            .Select(r => new
            {
                r.Type,
                Stats = r.Stats
                    .Where(st => st.Name == StatWins || st.Name == StatLosses)
                    .Select(st => new { st.Name, st.Value })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var overall = records.FirstOrDefault(r => r.Type == RecordTypeTotal);
        if (overall is null)
            return null;

        var overallWins = (int)(overall.Stats.FirstOrDefault(st => st.Name == StatWins)?.Value ?? 0);
        var overallLosses = (int)(overall.Stats.FirstOrDefault(st => st.Name == StatLosses)?.Value ?? 0);

        var conference = records.FirstOrDefault(r => r.Type == RecordTypeConference);

        var metricsResult = await _metricsHandler.ExecuteAsync(
            new GetFranchiseSeasonMetricsByIdQuery(priorSeason.Id), cancellationToken);

        return new PreviewPriorSeasonSummaryDto
        {
            SeasonYear = priorSeason.SeasonYear,
            Wins = overallWins,
            Losses = overallLosses,
            ConferenceWins = conference is null
                ? null
                : (int)(conference.Stats.FirstOrDefault(st => st.Name == StatWins)?.Value ?? 0),
            ConferenceLosses = conference is null
                ? null
                : (int)(conference.Stats.FirstOrDefault(st => st.Name == StatLosses)?.Value ?? 0),
            // NotFound = metrics simply not generated for that season — the
            // record still flows; the API's both-or-nothing rule handles
            // asymmetry before the model sees anything.
            Metrics = metricsResult.IsSuccess ? metricsResult.Value : null
        };
    }
}
