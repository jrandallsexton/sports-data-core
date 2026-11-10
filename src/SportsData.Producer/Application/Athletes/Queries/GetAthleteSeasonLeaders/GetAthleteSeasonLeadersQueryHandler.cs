using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Logos;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Application.Athletes.Queries.GetAthleteSeasonLeaders;

public interface IGetAthleteSeasonLeadersQueryHandler
{
    Task<Result<AthleteSeasonLeadersDto>> ExecuteAsync(
        GetAthleteSeasonLeadersQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Season leaderboard for one athlete stat, ranked from ESPN's season totals
/// (AthleteSeasonStatistic*), which the weekly franchise season enrichment
/// keeps current.
///
/// Data realities this handler accounts for:
///   - Athlete statistic docs are duplicated per re-source (most 2026 NCAAFB
///     athlete-seasons carry two); the NEWEST doc per athlete-season wins,
///     same rule as GetAthleteMatchupSummariesQueryHandler.
///   - ESPN's per-game values are sparse (about a quarter of passers carry
///     one), so per-game is computed as total / general.gamesPlayed.
///   - Per-game boards need a qualification floor or a one-game backup
///     leads; the NCAA rule (75% of the team's games) is applied against
///     the team's finalized non-preseason contests.
///
/// Only positive values rank: the first widgets are counting stats where
/// zero means "did not record one". Lower-is-better stats would need a
/// sort direction; none is supported yet.
/// </summary>
public class GetAthleteSeasonLeadersQueryHandler : IGetAthleteSeasonLeadersQueryHandler
{
    private const string GeneralCategory = "general";
    private const string GamesPlayedStat = "gamesPlayed";
    private const int PreseasonPhaseTypeCode = 1;
    private const decimal MinGamesPlayedShare = 0.75m;

    private readonly ILogger<GetAthleteSeasonLeadersQueryHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IValidator<GetAthleteSeasonLeadersQuery> _validator;
    private readonly IAppMode _appMode;
    private readonly ILogoSelectionService _logoSelectionService;

    public GetAthleteSeasonLeadersQueryHandler(
        ILogger<GetAthleteSeasonLeadersQueryHandler> logger,
        TeamSportDataContext dataContext,
        IValidator<GetAthleteSeasonLeadersQuery> validator,
        IAppMode appMode,
        ILogoSelectionService logoSelectionService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _validator = validator;
        _appMode = appMode;
        _logoSelectionService = logoSelectionService;
    }

    public async Task<Result<AthleteSeasonLeadersDto>> ExecuteAsync(
        GetAthleteSeasonLeadersQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<AthleteSeasonLeadersDto>(
                default!,
                ResultStatus.Validation,
                validation.Errors);
        }

        var dto = new AthleteSeasonLeadersDto
        {
            SeasonYear = query.SeasonYear,
            Category = query.Category,
            Stat = query.Stat,
            PerGame = query.PerGame,
            MinGamesPlayedShare = query.PerGame ? MinGamesPlayedShare : null,
        };

        // NCAAFB spans FBS/FCS/etc. and leaders are FBS only; other sports
        // (NFL) carry no classification in GroupSeasonMap.
        var requireFbs = _appMode.CurrentSport == Sport.FootballNcaa;

        // ── 1. The stat for every athlete-season in the season, newest doc only ──
        var rows = await (
                from c in _dataContext.AthleteSeasonStatisticCategories.AsNoTracking()
                where c.Name == query.Category
                from st in c.Stats
                where st.Name == query.Stat && st.Value != null && st.Value > 0
                join s in _dataContext.AthleteSeasonStatistics.AsNoTracking()
                    on c.AthleteSeasonStatisticId equals s.Id
                join a in _dataContext.AthleteSeasons.AsNoTracking()
                    on s.AthleteSeasonId equals a.Id
                join fs in _dataContext.FranchiseSeasons.AsNoTracking()
                    on a.FranchiseSeasonId equals fs.Id
                where fs.SeasonYear == query.SeasonYear &&
                      (!requireFbs || (fs.GroupSeasonMap != null && fs.GroupSeasonMap.Contains("fbs"))) &&
                      !_dataContext.AthleteSeasonStatistics.Any(o =>
                          o.AthleteSeasonId == s.AthleteSeasonId && o.CreatedUtc > s.CreatedUtc)
                select new
                {
                    AthleteSeasonId = a.Id,
                    DocId = s.Id,
                    FranchiseSeasonId = fs.Id,
                    Value = st.Value!.Value,
                    st.DisplayValue,
                    st.DisplayName,
                })
            .ToListAsync(cancellationToken);

        // Two docs with the same CreatedUtc both survive the filter above.
        var candidates = rows
            .GroupBy(r => r.AthleteSeasonId)
            .Select(g => g.OrderBy(r => r.DocId).First())
            .ToList();

        if (candidates.Count == 0)
        {
            return new Success<AthleteSeasonLeadersDto>(dto);
        }

        dto.StatDisplayName = candidates[0].DisplayName;

        // ── 2. Games played per doc (all candidates for per-game; it gates qualification) ──
        var docIds = candidates.Select(c => c.DocId).ToList();
        var gamesByDoc = (await _dataContext.AthleteSeasonStatisticCategories
                .AsNoTracking()
                .Where(c => docIds.Contains(c.AthleteSeasonStatisticId) && c.Name == GeneralCategory)
                .SelectMany(c => c.Stats
                    .Where(s => s.Name == GamesPlayedStat && s.Value != null)
                    .Select(s => new { c.AthleteSeasonStatisticId, Value = s.Value!.Value }))
                .ToListAsync(cancellationToken))
            .GroupBy(x => x.AthleteSeasonStatisticId)
            .ToDictionary(g => g.Key, g => (int)g.First().Value);

        // ── 3. Value to rank by ──────────────────────────────────────────
        List<(Guid AthleteSeasonId, Guid FranchiseSeasonId, decimal Value, string DisplayValue, int? Games)> ranked;

        if (query.PerGame)
        {
            var teamGames = await GetTeamGamesAsync(
                candidates.Select(c => c.FranchiseSeasonId).Distinct().ToList(),
                query.SeasonYear,
                cancellationToken);

            ranked = [];
            foreach (var c in candidates)
            {
                if (!gamesByDoc.TryGetValue(c.DocId, out var games) || games <= 0 ||
                    !teamGames.TryGetValue(c.FranchiseSeasonId, out var teamGameCount) ||
                    games < MinGamesPlayedShare * teamGameCount)
                {
                    continue;
                }

                var perGame = Math.Round(c.Value / games, 1, MidpointRounding.AwayFromZero);
                ranked.Add((c.AthleteSeasonId, c.FranchiseSeasonId, perGame, perGame.ToString("0.0"), games));
            }
        }
        else
        {
            ranked = candidates
                .Select(c => (c.AthleteSeasonId, c.FranchiseSeasonId, c.Value,
                    string.IsNullOrEmpty(c.DisplayValue) ? c.Value.ToString("0.##") : c.DisplayValue,
                    gamesByDoc.TryGetValue(c.DocId, out var g) ? g : (int?)null))
                .ToList();
        }

        // ── 4. Competition rank, then cut ────────────────────────────────
        var ordered = ranked
            .OrderByDescending(r => r.Value)
            .ThenBy(r => r.AthleteSeasonId)
            .ToList();

        var top = new List<(int Rank, Guid AthleteSeasonId, Guid FranchiseSeasonId, decimal Value, string DisplayValue, int? Games)>();
        for (var i = 0; i < ordered.Count && top.Count < query.Limit; i++)
        {
            var rank = i > 0 && ordered[i].Value == ordered[i - 1].Value ? top[^1].Rank : i + 1;
            var r = ordered[i];
            top.Add((rank, r.AthleteSeasonId, r.FranchiseSeasonId, r.Value, r.DisplayValue, r.Games));
        }

        // ── 5. Hydrate athletes and teams for the cut only ───────────────
        var topSeasonIds = top.Select(t => t.AthleteSeasonId).ToList();
        var athletes = await _dataContext.AthleteSeasons
            .AsNoTracking()
            .Where(a => topSeasonIds.Contains(a.Id))
            .Select(a => new
            {
                a.Id,
                a.AthleteId,
                a.DisplayName,
                a.FirstName,
                a.LastName,
                Position = a.Position.Abbreviation,
            })
            .ToDictionaryAsync(a => a.Id, cancellationToken);

        var topFsIds = top.Select(t => t.FranchiseSeasonId).Distinct().ToList();
        var teams = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(f => topFsIds.Contains(f.Id))
            .Select(f => new
            {
                f.Id,
                f.FranchiseId,
                f.Slug,
                f.Abbreviation,
                f.ColorCodeHex,
                f.DisplayNameShort,
                FranchiseShortName = _dataContext.Franchises
                    .Where(fr => fr.Id == f.FranchiseId)
                    .Select(fr => fr.DisplayNameShort)
                    .FirstOrDefault(),
            })
            .ToDictionaryAsync(f => f.Id, cancellationToken);

        var franchiseIds = teams.Values.Select(t => t.FranchiseId).Distinct().ToList();
        var franchiseLogos = (await _dataContext.FranchiseLogos
                .AsNoTracking()
                .Where(l => franchiseIds.Contains(l.FranchiseId))
                .ToListAsync(cancellationToken))
            .GroupBy(l => l.FranchiseId)
            .ToDictionary(g => g.Key, g => (IEnumerable<ILogo>)g.ToList());
        var seasonLogos = (await _dataContext.FranchiseSeasonLogos
                .AsNoTracking()
                .Where(l => topFsIds.Contains(l.FranchiseSeasonId))
                .ToListAsync(cancellationToken))
            .GroupBy(l => l.FranchiseSeasonId)
            .ToDictionary(g => g.Key, g => (IEnumerable<ILogo>)g.ToList());

        foreach (var t in top)
        {
            // A roster or team row can't vanish between queries in practice,
            // but a missing one drops the row rather than failing the board.
            if (!athletes.TryGetValue(t.AthleteSeasonId, out var athlete) ||
                !teams.TryGetValue(t.FranchiseSeasonId, out var team))
            {
                continue;
            }

            franchiseLogos.TryGetValue(team.FranchiseId, out var fLogos);
            seasonLogos.TryGetValue(team.Id, out var fsLogos);

            dto.Leaders.Add(new AthleteSeasonLeaderDto
            {
                Rank = t.Rank,
                AthleteId = athlete.AthleteId,
                AthleteName = !string.IsNullOrWhiteSpace(athlete.DisplayName)
                    ? athlete.DisplayName
                    : $"{athlete.FirstName} {athlete.LastName}".Trim(),
                Position = athlete.Position,
                FranchiseSlug = team.Slug,
                FranchiseShortName = team.FranchiseShortName ?? team.DisplayNameShort,
                FranchiseAbbreviation = team.Abbreviation,
                FranchiseColorHex = team.ColorCodeHex,
                FranchiseLogoUrl = _logoSelectionService.SelectWithFallback(fsLogos, fLogos)?.OriginalString,
                GamesPlayed = t.Games,
                Value = t.Value,
                DisplayValue = t.DisplayValue,
            });
        }

        // Ties read alphabetically within a rank.
        dto.Leaders = dto.Leaders
            .OrderBy(l => l.Rank)
            .ThenBy(l => l.AthleteName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Athlete season leaders: {Category}.{Stat} {SeasonYear} perGame={PerGame}; {Candidates} candidates, {Qualified} ranked, {Returned} returned.",
            query.Category, query.Stat, query.SeasonYear, query.PerGame,
            candidates.Count, ranked.Count, dto.Leaders.Count);

        return new Success<AthleteSeasonLeadersDto>(dto);
    }

    /// <summary>
    /// Finalized non-preseason contests per franchise season: the denominator
    /// for the per-game qualification floor.
    /// </summary>
    private async Task<Dictionary<Guid, int>> GetTeamGamesAsync(
        List<Guid> franchiseSeasonIds,
        int seasonYear,
        CancellationToken cancellationToken)
    {
        var contests = await (
                from c in _dataContext.Contests.AsNoTracking()
                join sp in _dataContext.SeasonPhases.AsNoTracking()
                    on c.SeasonPhaseId equals sp.Id
                where c.SeasonYear == seasonYear &&
                      c.FinalizedUtc != null &&
                      sp.TypeCode != PreseasonPhaseTypeCode &&
                      (franchiseSeasonIds.Contains(c.HomeTeamFranchiseSeasonId) ||
                       franchiseSeasonIds.Contains(c.AwayTeamFranchiseSeasonId))
                select new { c.HomeTeamFranchiseSeasonId, c.AwayTeamFranchiseSeasonId })
            .ToListAsync(cancellationToken);

        var counts = new Dictionary<Guid, int>();
        foreach (var c in contests)
        {
            counts[c.HomeTeamFranchiseSeasonId] = counts.GetValueOrDefault(c.HomeTeamFranchiseSeasonId) + 1;
            counts[c.AwayTeamFranchiseSeasonId] = counts.GetValueOrDefault(c.AwayTeamFranchiseSeasonId) + 1;
        }

        return counts;
    }
}
