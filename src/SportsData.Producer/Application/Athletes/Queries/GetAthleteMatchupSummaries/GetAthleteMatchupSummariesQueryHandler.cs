using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteMatchupSummaries;

public interface IGetAthleteMatchupSummariesQueryHandler
{
    Task<Result<AthleteMatchupSummariesDto>> ExecuteAsync(
        GetAthleteMatchupSummariesQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Athlete week-matchup summaries: every ACTIVE FBS athlete at a position,
/// with the week's opponent, the opponent's relevant defensive allowance
/// per game, and structured current/previous season stat blocks.
///
/// Built as set-based projected queries (athletes, prior seasons, stat
/// docs, stat rows, contests, opponent aggregates) — never per-athlete
/// round trips; a WR request touches ~700 athletes.
///
/// Two data realities shape this handler:
///   - ESPN's team-stat "yardsAllowed"/"pointsAllowed" are zero-filled,
///     so defensive allowances are AGGREGATED from what each opponent's
///     opponents actually gained against them (the OTHER side's
///     CompetitionCompetitor statistics in each played game). When an
///     opponent has no current-season games yet (week 1 everywhere), the
///     aggregate falls back to their prior-season franchise season.
///   - Athlete statistic docs are duplicated per re-source (~162k
///     athlete-seasons carry more than one); the NEWEST doc per
///     athlete-season wins.
/// </summary>
public class GetAthleteMatchupSummariesQueryHandler : IGetAthleteMatchupSummariesQueryHandler
{
    private const string GamesPlayedKey = "gamesPlayed";
    private const string GeneralCategory = "general";

    // Consumer-contract stat keys (the serialized shape downstream UIs
    // depend on — see AthleteMatchupSummaryDto) → the ESPN category/stat
    // names that carry them. gamesPlayed is added for every position at
    // query time.
    private static readonly Dictionary<string, Dictionary<string, (string Category, string Stat)>> StatMap;

    // The week opponent's defensive allowance metric per position. K is
    // points-allowed and comes from contest scores, not team stats.
    private static readonly Dictionary<string, (string Category, string Stat)> OppAllowedMap = new()
    {
        ["QB"] = ("passing", "netPassingYards"),
        ["WR"] = ("passing", "netPassingYards"),
        ["TE"] = ("passing", "netPassingYards"),
        ["RB"] = ("rushing", "rushingYards"),
    };

    static GetAthleteMatchupSummariesQueryHandler()
    {
        var qb = new Dictionary<string, (string, string)>
        {
            ["cmpPct"] = ("passing", "completionPct"),
            ["passYds"] = ("passing", "passingYards"),
            ["passYdsPerGame"] = ("passing", "passingYardsPerGame"),
            ["passTd"] = ("passing", "passingTouchdowns"),
            ["interceptions"] = ("passing", "interceptions"),
            ["rushYds"] = ("rushing", "rushingYards"),
        };
        var rb = new Dictionary<string, (string, string)>
        {
            ["rushAtt"] = ("rushing", "rushingAttempts"),
            ["rushYds"] = ("rushing", "rushingYards"),
            ["rushYdsPerGame"] = ("rushing", "rushingYardsPerGame"),
            ["rushTd"] = ("rushing", "rushingTouchdowns"),
            ["receptions"] = ("receiving", "receptions"),
        };
        var wr = new Dictionary<string, (string, string)>
        {
            ["receptions"] = ("receiving", "receptions"),
            ["recYds"] = ("receiving", "receivingYards"),
            ["recYdsPerGame"] = ("receiving", "receivingYardsPerGame"),
            ["recTd"] = ("receiving", "receivingTouchdowns"),
        };
        var k = new Dictionary<string, (string, string)>
        {
            ["fgMade"] = ("kicking", "fieldGoalsMade"),
            ["fgAtt"] = ("kicking", "fieldGoalAttempts"),
            ["fgPct"] = ("kicking", "fieldGoalPct"),
            ["fgLong"] = ("kicking", "longFieldGoalMade"),
            ["xpMade"] = ("kicking", "extraPointsMade"),
            ["xpAtt"] = ("kicking", "extraPointAttempts"),
        };
        StatMap = new Dictionary<string, Dictionary<string, (string, string)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["QB"] = qb,
            ["RB"] = rb,
            ["WR"] = wr,
            ["TE"] = wr, // TE shares WR's stat shape
            ["K"] = k,
        };
    }

    private readonly ILogger<GetAthleteMatchupSummariesQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IValidator<GetAthleteMatchupSummariesQuery> _validator;

    public GetAthleteMatchupSummariesQueryHandler(
        ILogger<GetAthleteMatchupSummariesQueryHandler> logger,
        TeamSportDataContext dataContext,
        IValidator<GetAthleteMatchupSummariesQuery> validator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _validator = validator;
    }

    public async Task<Result<AthleteMatchupSummariesDto>> ExecuteAsync(
        GetAthleteMatchupSummariesQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<AthleteMatchupSummariesDto>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        // Resolve the CANONICAL whitelist key rather than echoing the raw
        // input: the position used everywhere downstream (including logs)
        // is provably one of the dictionary's compile-time constants, and
        // the whitelist and the stat mapping stay one dictionary.
        var position = StatMap.Keys.FirstOrDefault(
            k => string.Equals(k, query.Position, StringComparison.OrdinalIgnoreCase));
        if (position is null)
        {
            return new Failure<AthleteMatchupSummariesDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.Position),
                    $"Unsupported position '{query.Position}'. Expected one of: {string.Join(", ", StatMap.Keys)}.")]);
        }

        var statKeys = StatMap[position];

        // The UI contract says "K"; ESPN's position row is "PK" (Place
        // Kicker). Translate at the boundary so both sides keep their
        // native vocabulary.
        var dbPosition = position == "K" ? "PK" : position;

        // ── 1. Active FBS athletes at the position for the season ─────────
        // Explicit join: AthleteSeason carries FranchiseSeasonId with no
        // navigation property.
        var athletes = await (
                from a in _dataContext.AthleteSeasons.AsNoTracking()
                join fs in _dataContext.FranchiseSeasons.AsNoTracking()
                    on a.FranchiseSeasonId equals fs.Id
                where a.IsActive &&
                      a.Position.Abbreviation == dbPosition &&
                      a.Status != null && a.Status.Name == "Active" &&
                      fs.SeasonYear == query.SeasonYear &&
                      fs.GroupSeasonMap != null &&
                      fs.GroupSeasonMap.Contains("fbs")
                select new
                {
                    AthleteSeasonId = a.Id,
                    a.AthleteId,
                    a.FirstName,
                    a.LastName,
                    FranchiseSeasonId = fs.Id,
                    // DisplayName, not Name: Name is the bare mascot and
                    // NCAAFB has ten different Tigers.
                    TeamName = fs.DisplayName,
                    TeamSlug = fs.Slug,
                })
            .ToListAsync(cancellationToken);

        if (athletes.Count == 0)
        {
            return new Success<AthleteMatchupSummariesDto>(new AthleteMatchupSummariesDto());
        }

        var athleteIds = athletes.Select(a => a.AthleteId).ToList();
        var teamFsIds = athletes.Select(a => a.FranchiseSeasonId).Distinct().ToList();

        // ── 2. Prior-season athlete rows for the sub-row stat block ───────
        var priorYear = query.SeasonYear - 1;
        var priorSeasons = await (
                from a in _dataContext.AthleteSeasons.AsNoTracking()
                join fs in _dataContext.FranchiseSeasons.AsNoTracking()
                    on a.FranchiseSeasonId equals fs.Id
                where athleteIds.Contains(a.AthleteId) &&
                      fs.SeasonYear == priorYear
                select new { a.AthleteId, AthleteSeasonId = a.Id })
            .ToListAsync(cancellationToken);

        // A transfer mid-cycle can leave two prior rows; keep one.
        var priorByAthlete = priorSeasons
            .GroupBy(p => p.AthleteId)
            .ToDictionary(g => g.Key, g => g.First().AthleteSeasonId);

        // ── 3. Newest statistic doc per athlete-season (dupes are real) ───
        var allSeasonIds = athletes.Select(a => a.AthleteSeasonId)
            .Concat(priorByAthlete.Values)
            .ToList();

        var statDocs = await _dataContext.AthleteSeasonStatistics
            .AsNoTracking()
            .Where(s => allSeasonIds.Contains(s.AthleteSeasonId))
            .Select(s => new { s.Id, s.AthleteSeasonId, s.CreatedUtc })
            .ToListAsync(cancellationToken);

        var newestDocBySeason = statDocs
            .GroupBy(s => s.AthleteSeasonId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.CreatedUtc).First().Id);

        var docIds = newestDocBySeason.Values.ToList();
        var seasonByDoc = newestDocBySeason.ToDictionary(kv => kv.Value, kv => kv.Key);

        // ── 4. Stat rows for those docs, filtered to the contract's keys ──
        var wantedCats = statKeys.Values.Select(v => v.Category)
            .Append(GeneralCategory).Distinct().ToList();
        var wantedStats = statKeys.Values.Select(v => v.Stat)
            .Append(GamesPlayedKey).Distinct().ToList();

        var statRows = await _dataContext.AthleteSeasonStatisticCategories
            .AsNoTracking()
            .Where(c => docIds.Contains(c.AthleteSeasonStatisticId) && wantedCats.Contains(c.Name))
            .SelectMany(c => c.Stats
                .Where(s => wantedStats.Contains(s.Name) && s.Value != null)
                .Select(s => new
                {
                    c.AthleteSeasonStatisticId,
                    Category = c.Name,
                    Stat = s.Name,
                    Value = s.Value!.Value,
                }))
            .ToListAsync(cancellationToken);

        // (athleteSeasonId, category, stat) → value. Same-named stats recur
        // across categories (scoring repeats rushingTouchdowns), so the
        // category is part of the key.
        var statLookup = new Dictionary<(Guid, string, string), decimal>();
        foreach (var row in statRows)
        {
            statLookup[(seasonByDoc[row.AthleteSeasonStatisticId], row.Category, row.Stat)] = row.Value;
        }

        // ── 5. The week's opponent per team ───────────────────────────────
        // Week resolves through SeasonWeek.Number, NOT Contest.Week: the
        // schedule import leaves Week null until the companion doc
        // hydrates it (all 1,527 local 2026 contests were null), while
        // SeasonWeekId is populated on every one of them.
        //
        // Scoped to the REGULAR SEASON phase (TypeCode 2): week numbers
        // restart per phase, so an unscoped Number match would let a
        // postseason "week 1" overwrite the real week-1 opponent.
        const int regularSeasonTypeCode = 2;
        var weekContests = await (
                from c in _dataContext.Contests.AsNoTracking()
                join sw in _dataContext.SeasonWeeks.AsNoTracking()
                    on c.SeasonWeekId equals sw.Id
                join sp in _dataContext.SeasonPhases.AsNoTracking()
                    on sw.SeasonPhaseId equals sp.Id
                where c.SeasonYear == query.SeasonYear &&
                      sw.Number == query.Week &&
                      sp.TypeCode == regularSeasonTypeCode &&
                      c.CancelledUtc == null &&
                      (teamFsIds.Contains(c.HomeTeamFranchiseSeasonId) ||
                       teamFsIds.Contains(c.AwayTeamFranchiseSeasonId))
                select new { c.HomeTeamFranchiseSeasonId, c.AwayTeamFranchiseSeasonId })
            .ToListAsync(cancellationToken);

        var opponentByTeam = new Dictionary<Guid, Guid>();
        foreach (var c in weekContests)
        {
            // Both sides may be FBS; register each direction we care about.
            if (teamFsIds.Contains(c.HomeTeamFranchiseSeasonId))
                opponentByTeam[c.HomeTeamFranchiseSeasonId] = c.AwayTeamFranchiseSeasonId;
            if (teamFsIds.Contains(c.AwayTeamFranchiseSeasonId))
                opponentByTeam[c.AwayTeamFranchiseSeasonId] = c.HomeTeamFranchiseSeasonId;
        }

        var opponentFsIds = opponentByTeam.Values.Distinct().ToList();

        var opponentInfo = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(f => opponentFsIds.Contains(f.Id))
            // DisplayName, not Name — same ten-Tigers problem as above.
            .Select(f => new { f.Id, Name = f.DisplayName, f.Slug, f.FranchiseId })
            .ToListAsync(cancellationToken);
        var opponentById = opponentInfo.ToDictionary(o => o.Id);

        // ── 6. Opponent defensive allowance per game ──────────────────────
        // Current-season aggregate first; opponents with no games yet fall
        // back to their prior-season franchise season.
        var allowedByOpponent = position == "K"
            ? await ComputePointsAllowedAsync(opponentFsIds, cancellationToken)
            : await ComputeYardsAllowedAsync(opponentFsIds, OppAllowedMap[position], cancellationToken);

        var missing = opponentFsIds.Where(id => !allowedByOpponent.ContainsKey(id)).ToList();
        if (missing.Count > 0)
        {
            var priorFsByFranchise = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(f => f.SeasonYear == priorYear &&
                            opponentInfo.Select(o => o.FranchiseId).Contains(f.FranchiseId))
                .Select(f => new { f.Id, f.FranchiseId })
                .ToListAsync(cancellationToken);

            var priorFsLookup = priorFsByFranchise
                .GroupBy(f => f.FranchiseId)
                .ToDictionary(g => g.Key, g => g.First().Id);

            // TryGetValue throughout: a Contest can reference a
            // FranchiseSeasonId with no FranchiseSeason row (unsourced
            // opponent), and this block runs on every week-1 request — a
            // direct index would turn that data gap into a 500 instead of
            // a row with a null allowance.
            var missingPriorIds = missing
                .Where(id => opponentById.TryGetValue(id, out var info) &&
                             priorFsLookup.ContainsKey(info.FranchiseId))
                .Select(id => priorFsLookup[opponentById[id].FranchiseId])
                .Distinct()
                .ToList();

            var priorAllowed = position == "K"
                ? await ComputePointsAllowedAsync(missingPriorIds, cancellationToken)
                : await ComputeYardsAllowedAsync(missingPriorIds, OppAllowedMap[position], cancellationToken);

            foreach (var id in missing)
            {
                if (opponentById.TryGetValue(id, out var info) &&
                    priorFsLookup.TryGetValue(info.FranchiseId, out var priorId) &&
                    priorAllowed.TryGetValue(priorId, out var value))
                {
                    allowedByOpponent[id] = value;
                }
            }
        }

        // ── 7. Assemble ───────────────────────────────────────────────────
        var dto = new AthleteMatchupSummariesDto();
        foreach (var a in athletes.OrderBy(x => x.LastName ?? string.Empty).ThenBy(x => x.FirstName ?? string.Empty))
        {
            Guid? oppId = opponentByTeam.TryGetValue(a.FranchiseSeasonId, out var o) ? o : null;
            var opp = oppId.HasValue && opponentById.TryGetValue(oppId.Value, out var info) ? info : null;

            dto.Athletes.Add(new AthleteMatchupSummaryDto
            {
                AthleteId = a.AthleteId,
                FirstName = a.FirstName ?? string.Empty,
                LastName = a.LastName ?? string.Empty,
                TeamName = a.TeamName ?? a.TeamSlug ?? string.Empty,
                TeamSlug = a.TeamSlug ?? string.Empty,
                Position = position,
                OpponentName = opp?.Name,
                OpponentSlug = opp?.Slug,
                OpponentDefPerGame = oppId.HasValue && allowedByOpponent.TryGetValue(oppId.Value, out var allowed)
                    ? allowed
                    : null,
                CurrentSeason = BuildSeasonBlock(a.AthleteSeasonId, query.SeasonYear, statKeys, statLookup),
                PreviousSeason = priorByAthlete.TryGetValue(a.AthleteId, out var priorSeasonId)
                    ? BuildSeasonBlock(priorSeasonId, priorYear, statKeys, statLookup)
                    : null,
            });
        }

        _logger.LogInformation(
            "Athlete matchup summaries: {Count} {Position} rows for {SeasonYear} week {Week}; {WithOpp} with opponents, {WithAllowed} with allowance data.",
            dto.Athletes.Count, position, query.SeasonYear, query.Week,
            dto.Athletes.Count(x => x.OpponentName != null),
            dto.Athletes.Count(x => x.OpponentDefPerGame != null));

        return new Success<AthleteMatchupSummariesDto>(dto);
    }

    /// <summary>
    /// Null when the athlete-season has no statistic doc or shows zero
    /// games played — that is "hasn't played", which the grid renders as
    /// em-dashes and sinks under stat sorts.
    /// </summary>
    private static AthleteSeasonStatBlockDto? BuildSeasonBlock(
        Guid athleteSeasonId,
        int seasonYear,
        Dictionary<string, (string Category, string Stat)> statKeys,
        Dictionary<(Guid, string, string), decimal> statLookup)
    {
        if (!statLookup.TryGetValue((athleteSeasonId, GeneralCategory, GamesPlayedKey), out var games) || games <= 0)
        {
            return null;
        }

        var block = new AthleteSeasonStatBlockDto
        {
            SeasonYear = seasonYear,
            GamesPlayed = (int)games,
        };

        foreach (var (key, (category, stat)) in statKeys)
        {
            if (statLookup.TryGetValue((athleteSeasonId, category, stat), out var value))
            {
                block.Stats[key] = value;
            }
        }

        return block;
    }

    /// <summary>
    /// Per-game average of a stat gained AGAINST each franchise season —
    /// the other side's team statistics across that team's played games.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> ComputeYardsAllowedAsync(
        List<Guid> franchiseSeasonIds,
        (string Category, string Stat) metric,
        CancellationToken cancellationToken)
    {
        if (franchiseSeasonIds.Count == 0) return new Dictionary<Guid, decimal>();

        var rows = await (
                from cc in _dataContext.CompetitionCompetitors.AsNoTracking()
                where franchiseSeasonIds.Contains(cc.FranchiseSeasonId)
                join s in _dataContext.CompetitionCompetitorStatistics.AsNoTracking()
                    on cc.CompetitionId equals s.CompetitionId
                where s.FranchiseSeasonId != cc.FranchiseSeasonId
                join cat in _dataContext.CompetitionCompetitorStatisticCategories.AsNoTracking()
                    on s.Id equals cat.CompetitionCompetitorStatisticId
                where cat.Name == metric.Category
                join st in _dataContext.CompetitionCompetitorStatisticStats.AsNoTracking()
                    on cat.Id equals st.CompetitionCompetitorStatisticCategoryId
                where st.Name == metric.Stat && st.Value != null
                select new { cc.FranchiseSeasonId, Value = st.Value!.Value })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.FranchiseSeasonId)
            .ToDictionary(g => g.Key, g => Math.Round(g.Average(r => r.Value), 1));
    }

    /// <summary>
    /// Per-game points allowed from finalized contest scores — the K
    /// opponent metric. Contest scores are authoritative and exist even
    /// where per-game team statistics have gaps.
    /// </summary>
    private async Task<Dictionary<Guid, decimal>> ComputePointsAllowedAsync(
        List<Guid> franchiseSeasonIds,
        CancellationToken cancellationToken)
    {
        if (franchiseSeasonIds.Count == 0) return new Dictionary<Guid, decimal>();

        var contests = await _dataContext.Contests
            .AsNoTracking()
            .Where(c =>
                c.FinalizedUtc != null &&
                c.HomeScore != null && c.AwayScore != null &&
                (franchiseSeasonIds.Contains(c.HomeTeamFranchiseSeasonId) ||
                 franchiseSeasonIds.Contains(c.AwayTeamFranchiseSeasonId)))
            .Select(c => new
            {
                c.HomeTeamFranchiseSeasonId,
                c.AwayTeamFranchiseSeasonId,
                c.HomeScore,
                c.AwayScore,
            })
            .ToListAsync(cancellationToken);

        var pointsAllowed = new Dictionary<Guid, List<int>>();
        foreach (var c in contests)
        {
            if (franchiseSeasonIds.Contains(c.HomeTeamFranchiseSeasonId))
            {
                pointsAllowed.TryAdd(c.HomeTeamFranchiseSeasonId, []);
                pointsAllowed[c.HomeTeamFranchiseSeasonId].Add(c.AwayScore!.Value);
            }
            if (franchiseSeasonIds.Contains(c.AwayTeamFranchiseSeasonId))
            {
                pointsAllowed.TryAdd(c.AwayTeamFranchiseSeasonId, []);
                pointsAllowed[c.AwayTeamFranchiseSeasonId].Add(c.HomeScore!.Value);
            }
        }

        return pointsAllowed.ToDictionary(
            kv => kv.Key,
            kv => Math.Round((decimal)kv.Value.Average(), 1));
    }
}
