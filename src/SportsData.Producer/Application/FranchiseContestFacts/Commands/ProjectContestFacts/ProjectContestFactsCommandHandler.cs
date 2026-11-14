using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;

public interface IProjectContestFactsCommandHandler
{
    /// <summary>Returns the number of fact rows the contest now has (0 or 2).</summary>
    Task<Result<int>> ExecuteAsync(ProjectContestFactsCommand command, CancellationToken cancellationToken = default);
}

/// <summary>
/// Loads one contest's raw facts, derives its two
/// <see cref="FranchiseContestFact"/> rows and upserts them. Idempotent by
/// (ContestId, FranchiseSeasonId); a contest that is no longer projectable
/// (not finalized, cancelled, out of scope) has its rows removed, so a
/// re-run always converges on what the source data says.
///
/// Scope: finalized, non-cancelled, non-preseason games with a Competition
/// row; for NCAAFB only games with at least one FBS team (owner decision
/// 2026-11-12).
/// </summary>
public class ProjectContestFactsCommandHandler : IProjectContestFactsCommandHandler
{
    private const int PreseasonPhaseTypeCode = 1;

    private readonly ILogger<ProjectContestFactsCommandHandler> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IAppMode _appMode;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ProjectContestFactsCommandHandler(
        ILogger<ProjectContestFactsCommandHandler> logger,
        FootballDataContext dataContext,
        IAppMode appMode,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _appMode = appMode;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<int>> ExecuteAsync(
        ProjectContestFactsCommand command,
        CancellationToken cancellationToken = default)
    {
        var contest = await _dataContext.Contests
            .AsNoTracking()
            .Where(c => c.Id == command.ContestId)
            .Select(c => new
            {
                c.Id,
                c.SeasonYear,
                c.Week,
                c.SeasonPhaseId,
                PhaseTypeCode = _dataContext.SeasonPhases
                    .Where(sp => sp.Id == c.SeasonPhaseId)
                    .Select(sp => (int?)sp.TypeCode)
                    .FirstOrDefault(),
                c.StartDateUtc,
                c.VenueId,
                c.HomeTeamFranchiseSeasonId,
                c.AwayTeamFranchiseSeasonId,
                c.HomeScore,
                c.AwayScore,
                c.WinnerFranchiseSeasonId,
                c.SpreadWinnerFranchiseSeasonId,
                c.OverUnder,
                c.FinalizedUtc,
                c.CancelledUtc,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (contest is null)
        {
            return new Failure<int>(0, ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ContestId), "Contest not found")]);
        }

        var teams = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(fs => fs.Id == contest.HomeTeamFranchiseSeasonId || fs.Id == contest.AwayTeamFranchiseSeasonId)
            .Select(fs => new
            {
                fs.Id,
                fs.FranchiseId,
                fs.GroupSeasonMap,
                // Same conference identity as EnrichFranchiseSeasonHandler:
                // a division's parent conference, else the group itself.
                ConferenceGroupId = fs.GroupSeason != null &&
                                    !fs.GroupSeason.IsConference &&
                                    fs.GroupSeason.Parent != null &&
                                    fs.GroupSeason.Parent.IsConference
                    ? fs.GroupSeason.ParentId
                    : fs.GroupSeasonId,
            })
            .ToDictionaryAsync(fs => fs.Id, cancellationToken);

        var inScope =
            contest.FinalizedUtc is not null &&
            contest.CancelledUtc is null &&
            contest.PhaseTypeCode != PreseasonPhaseTypeCode &&
            teams.ContainsKey(contest.HomeTeamFranchiseSeasonId) &&
            teams.ContainsKey(contest.AwayTeamFranchiseSeasonId) &&
            (_appMode.CurrentSport != Sport.FootballNcaa ||
             IsFbs(teams[contest.HomeTeamFranchiseSeasonId].GroupSeasonMap) ||
             IsFbs(teams[contest.AwayTeamFranchiseSeasonId].GroupSeasonMap));

        if (!inScope)
        {
            var removed = await RemoveFactsAsync(contest.Id, cancellationToken);
            _logger.LogInformation(
                "Contest not projectable; removed {Removed} fact rows. ContestId={ContestId}",
                removed, contest.Id);
            return new Success<int>(0);
        }

        // Contest:Competition is 1:1 in practice; take a deterministic one.
        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .Where(k => k.ContestId == contest.Id)
            .OrderBy(k => k.Id)
            .Select(k => new
            {
                k.Id,
                k.TimeValid,
                k.IsNeutralSite,
                k.IsConferenceCompetition,
                k.VenueId,
                IsChampionship = k.Notes.Any(n => n.Headline.ToLower().Contains("championship")),
            })
            .FirstOrDefaultAsync(cancellationToken);

        // Site, conference game and kickoff validity all come from the
        // Competition. Without one (not ingested yet) the row would state
        // Home/Away and a conference answer it cannot know, so the contest
        // is not projectable yet; a later run picks it up.
        if (competition is null)
        {
            var removed = await RemoveFactsAsync(contest.Id, cancellationToken);
            _logger.LogInformation(
                "Contest has no competition; not projectable yet. Removed {Removed} fact rows. ContestId={ContestId}",
                removed, contest.Id);
            return new Success<int>(0);
        }

        var odds = await _dataContext.CompetitionOdds
            .AsNoTracking()
            .Where(o => o.CompetitionId == competition.Id)
            .ToListAsync(cancellationToken);
        var primaryOdds = OddsProviderPreference.SelectPrimary(odds);

        var ranks = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .OfType<FootballCompetitionCompetitor>()
            .Where(cc => cc.CompetitionId == competition.Id)
            .GroupBy(cc => cc.FranchiseSeasonId)
            .Select(g => new { FranchiseSeasonId = g.Key, Rank = g.Max(cc => cc.CuratedRankCurrent) })
            .ToDictionaryAsync(x => x.FranchiseSeasonId, x => x.Rank, cancellationToken);

        var seasonUsesConferenceFlag = await SeasonUsesConferenceFlagAsync(contest.SeasonYear, cancellationToken);

        var venueId = contest.VenueId ?? competition.VenueId;
        var venueTimeZone = venueId is null
            ? null
            : await _dataContext.Venues
                .AsNoTracking()
                .Where(v => v.Id == venueId)
                .Select(v => v.TimeZone)
                .FirstOrDefaultAsync(cancellationToken);

        var records = await GetRecordsBeforeAsync(
            [contest.HomeTeamFranchiseSeasonId, contest.AwayTeamFranchiseSeasonId],
            contest.StartDateUtc,
            cancellationToken);

        var boxes = await GetBoxesAsync(competition.Id, cancellationToken);

        TeamFactInput Team(Guid franchiseSeasonId)
        {
            var team = teams[franchiseSeasonId];
            var record = records.GetValueOrDefault(franchiseSeasonId);
            return new TeamFactInput
            {
                FranchiseSeasonId = team.Id,
                FranchiseId = team.FranchiseId,
                GroupSeasonMap = team.GroupSeasonMap,
                ConferenceGroupId = team.ConferenceGroupId,
                // NCAAFB only: ESPN gives every NFL competitor 99, which is
                // not a poll position.
                CuratedRank = _appMode.CurrentSport == Sport.FootballNcaa
                    ? ranks.GetValueOrDefault(franchiseSeasonId)
                    : null,
                WinsBefore = record.Wins,
                LossesBefore = record.Losses,
                TiesBefore = record.Ties,
                Box = boxes.GetValueOrDefault(franchiseSeasonId),
            };
        }

        var input = new ContestFactInput
        {
            ContestId = contest.Id,
            SeasonYear = contest.SeasonYear,
            Week = contest.Week,
            SeasonPhaseTypeCode = contest.PhaseTypeCode,
            KickoffUtc = contest.StartDateUtc,
            KickoffTimeKnown = competition.TimeValid,
            VenueId = venueId,
            VenueTimeZone = venueTimeZone,
            IsNeutralSite = competition.IsNeutralSite,
            EspnConferenceFlag = competition.IsConferenceCompetition,
            SeasonUsesConferenceFlag = seasonUsesConferenceFlag,
            IsChampionship = competition.IsChampionship,
            HomeScore = contest.HomeScore,
            AwayScore = contest.AwayScore,
            WinnerFranchiseSeasonId = contest.WinnerFranchiseSeasonId,
            SpreadWinnerFranchiseSeasonId = contest.SpreadWinnerFranchiseSeasonId,
            HomeSpread = primaryOdds?.Spread,
            OverUnder = primaryOdds?.OverUnder,
            TotalResult = contest.OverUnder,
            Home = Team(contest.HomeTeamFranchiseSeasonId),
            Away = Team(contest.AwayTeamFranchiseSeasonId),
        };

        var facts = FranchiseContestFactProjector.Project(input, _dateTimeProvider.UtcNow());
        await UpsertAsync(contest.Id, facts, cancellationToken);

        return new Success<int>(facts.Count);
    }

    private static bool IsFbs(string? groupSeasonMap) =>
        groupSeasonMap is not null && groupSeasonMap.Contains("fbs");

    /// <summary>
    /// The conference record's switch: a season uses ESPN's flag only when a
    /// real share (10%) of its games carry it.
    /// </summary>
    private async Task<bool> SeasonUsesConferenceFlagAsync(int seasonYear, CancellationToken cancellationToken)
    {
        var seasonCompetitions = _dataContext.Competitions
            .AsNoTracking()
            .Where(k => k.Contest.SeasonYear == seasonYear);

        var total = await seasonCompetitions.CountAsync(cancellationToken);
        var flagged = await seasonCompetitions.CountAsync(k => k.IsConferenceCompetition, cancellationToken);

        return flagged > 0 && flagged * 10 >= total;
    }

    /// <summary>
    /// W-L-T before kickoff, counted like the season record: finalized games
    /// of the same team-season, preseason excluded (an unmatched phase is
    /// kept), a finalized game without a winner is a tie.
    /// </summary>
    private async Task<Dictionary<Guid, (int Wins, int Losses, int Ties)>> GetRecordsBeforeAsync(
        List<Guid> franchiseSeasonIds,
        DateTime kickoffUtc,
        CancellationToken cancellationToken)
    {
        var prior = await _dataContext.Contests
            .AsNoTracking()
            .Where(c => c.FinalizedUtc != null &&
                        c.StartDateUtc < kickoffUtc &&
                        (franchiseSeasonIds.Contains(c.HomeTeamFranchiseSeasonId) ||
                         franchiseSeasonIds.Contains(c.AwayTeamFranchiseSeasonId)))
            .Where(c => !_dataContext.SeasonPhases.Any(sp => sp.Id == c.SeasonPhaseId && sp.TypeCode == PreseasonPhaseTypeCode))
            .Select(c => new { c.HomeTeamFranchiseSeasonId, c.AwayTeamFranchiseSeasonId, c.WinnerFranchiseSeasonId })
            .ToListAsync(cancellationToken);

        var records = new Dictionary<Guid, (int Wins, int Losses, int Ties)>();
        foreach (var id in franchiseSeasonIds)
        {
            var games = prior.Where(c => c.HomeTeamFranchiseSeasonId == id || c.AwayTeamFranchiseSeasonId == id).ToList();
            records[id] = (
                games.Count(c => c.WinnerFranchiseSeasonId == id),
                games.Count(c => c.WinnerFranchiseSeasonId is not null && c.WinnerFranchiseSeasonId != id),
                games.Count(c => c.WinnerFranchiseSeasonId is null));
        }

        return records;
    }

    /// <summary>
    /// Team box per competitor from ESPN team statistics (category + stat
    /// names confirmed against prod data, 2026-11-12). Newest document per
    /// team if there are ever two.
    /// </summary>
    private async Task<Dictionary<Guid, TeamBoxInput>> GetBoxesAsync(Guid competitionId, CancellationToken cancellationToken)
    {
        var docs = await _dataContext.CompetitionCompetitorStatistics
            .AsNoTracking()
            .Where(s => s.CompetitionId == competitionId)
            .OrderByDescending(s => s.CreatedUtc)
            .Select(s => new
            {
                s.FranchiseSeasonId,
                Stats = s.Categories
                    .SelectMany(cat => cat.Stats.Select(st => new { Category = cat.Name, st.Name, st.Value }))
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        var boxes = new Dictionary<Guid, TeamBoxInput>();
        foreach (var doc in docs)
        {
            if (boxes.ContainsKey(doc.FranchiseSeasonId))
                continue;

            var values = doc.Stats
                .Where(s => s.Value is not null)
                .GroupBy(s => (s.Category, s.Name))
                .ToDictionary(g => g.Key, g => g.First().Value!.Value);

            int? Get(string category, string name) =>
                values.TryGetValue((category, name), out var v) ? (int)Math.Round(v) : null;

            boxes[doc.FranchiseSeasonId] = new TeamBoxInput
            {
                TotalYards = Get("passing", "netTotalYards"),
                PassingYards = Get("passing", "netPassingYards"),
                RushingYards = Get("rushing", "rushingYards"),
                PassAttempts = Get("passing", "passingAttempts"),
                Completions = Get("passing", "completions"),
                RushAttempts = Get("rushing", "rushingAttempts"),
                TotalPlays = Get("miscellaneous", "totalPlays"),
                FirstDowns = Get("miscellaneous", "firstDowns"),
                ThirdDownConversions = Get("miscellaneous", "thirdDownConvs"),
                ThirdDownAttempts = Get("miscellaneous", "thirdDownAttempts"),
                FourthDownConversions = Get("miscellaneous", "fourthDownConvs"),
                FourthDownAttempts = Get("miscellaneous", "fourthDownAttempts"),
                RedZoneAttempts = Get("miscellaneous", "redzoneAttempts"),
                RedZoneScores = Get("miscellaneous", "redzoneConversion"),
                Giveaways = Get("miscellaneous", "totalGiveaways"),
                Takeaways = Get("miscellaneous", "totalTakeaways"),
                Penalties = Get("miscellaneous", "totalPenalties"),
                PenaltyYards = Get("miscellaneous", "totalPenaltyYards"),
                PossessionSeconds = Get("miscellaneous", "possessionTimeSeconds"),
                SacksMade = Get("defensive", "sacks"),
                SacksAllowed = Get("passing", "sacks"),
            };
        }

        return boxes;
    }

    private async Task UpsertAsync(Guid contestId, IReadOnlyList<FranchiseContestFact> facts, CancellationToken cancellationToken)
    {
        var existing = await _dataContext.FranchiseContestFacts
            .Where(f => f.ContestId == contestId)
            .ToListAsync(cancellationToken);

        foreach (var stale in existing.Where(e => facts.All(f => f.FranchiseSeasonId != e.FranchiseSeasonId)))
        {
            _dataContext.FranchiseContestFacts.Remove(stale);
        }

        foreach (var fact in facts)
        {
            var row = existing.FirstOrDefault(e => e.FranchiseSeasonId == fact.FranchiseSeasonId);
            if (row is null)
                _dataContext.FranchiseContestFacts.Add(fact);
            else
                _dataContext.Entry(row).CurrentValues.SetValues(fact);
        }

        await _dataContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<int> RemoveFactsAsync(Guid contestId, CancellationToken cancellationToken)
    {
        var existing = await _dataContext.FranchiseContestFacts
            .Where(f => f.ContestId == contestId)
            .ToListAsync(cancellationToken);

        if (existing.Count == 0)
            return 0;

        _dataContext.FranchiseContestFacts.RemoveRange(existing);
        await _dataContext.SaveChangesAsync(cancellationToken);
        return existing.Count;
    }
}
