using System.Diagnostics;
using System.Text.Json;

using MassTransit.EntityFrameworkCoreIntegration;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions.InstantAnalysis;

/// <summary>A play loaded for analysis, with its participants.</summary>
public record AnalysedPlay(
    Guid PlayId,
    Guid ContestId,
    Guid CompetitionId,
    string SequenceNumber,
    PlayType Type,
    string TypeId,
    string Text,
    int StatYardage,
    Guid? StartFranchiseSeasonId,
    IReadOnlyList<AnalysedPlayParticipant> Participants);

public record AnalysedPlayParticipant(
    Guid? AthleteSeasonId,
    string Type,
    int Order);

/// <summary>Which season document a season fact may read (task 06).</summary>
public enum SeasonDocumentRule
{
    /// <summary>
    /// Each athlete's newest document. Not used by live or the lab: a document
    /// rewritten mid-game can already include part of the game, which the
    /// season fact would then count twice (data spike, 01-data-spike-findings.md).
    /// </summary>
    Newest,

    /// <summary>
    /// Live and lab replay: only a document written before the game's kickoff,
    /// which can't include the game. No such document drops the season fact.
    /// </summary>
    BeforeKickoff
}

public enum PlayAnalysisStatus
{
    Analysed,

    /// <summary>The play already has a fact set for this run (redelivery).</summary>
    Skipped,

    NotFound
}

public record PlayAnalysisOutcome(
    PlayAnalysisStatus Status,
    int Generated,
    int Persisted,
    IReadOnlyList<DroppedFact> Dropped)
{
    public static readonly PlayAnalysisOutcome Skipped = new(PlayAnalysisStatus.Skipped, 0, 0, []);
    public static readonly PlayAnalysisOutcome NotFound = new(PlayAnalysisStatus.NotFound, 0, 0, []);
}

public interface IPlayAnalyser
{
    /// <summary>
    /// Analyses one play and persists its fact set for the run. Live passes no
    /// run id, a lab replay passes its run id; both use
    /// <see cref="SeasonDocumentRule.BeforeKickoff"/>. Doesn't check the
    /// per-game flag: callers gate.
    /// </summary>
    Task<PlayAnalysisOutcome> Analyse(
        Guid playId,
        Guid contestId,
        Guid correlationId,
        Guid? analysisRunId,
        SeasonDocumentRule documentRule);
}

/// <summary>
/// The instant-analysis pipeline for one play: skips a play that already has a
/// fact set for the run (at-least-once redelivery), loads the play and its
/// participants, classifies it (task 04), computes game facts from the game's
/// own plays (task 05) and season facts from the athletes' season documents
/// plus this game (task 06), selects, orders and phrases them (task 07), and
/// persists the set as PlayAnalysis rows, logging every decision and per-stage
/// timings (task 08). Used by the live job and the replay lab (task 09).
/// </summary>
public class PlayAnalyser : IPlayAnalyser
{
    private readonly ILogger<PlayAnalyser> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IEventBus _eventBus;

    public PlayAnalyser(
        ILogger<PlayAnalyser> logger,
        FootballDataContext dataContext,
        IDateTimeProvider dateTimeProvider,
        IEventBus eventBus)
    {
        _logger = logger;
        _dataContext = dataContext;
        _dateTimeProvider = dateTimeProvider;
        _eventBus = eventBus;
    }

    public async Task<PlayAnalysisOutcome> Analyse(
        Guid playId,
        Guid contestId,
        Guid correlationId,
        Guid? analysisRunId,
        SeasonDocumentRule documentRule)
    {
        using var _ = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["PlayId"] = playId,
            ["AnalysisRunId"] = analysisRunId
        });

        var alreadyAnalysed = await _dataContext.PlayAnalyses
            .AsNoTracking()
            .AnyAsync(x => x.PlayId == playId && x.AnalysisRunId == analysisRunId);

        if (alreadyAnalysed)
        {
            _logger.LogInformation("Play already has a fact set for this run; skipping redelivery.");
            return PlayAnalysisOutcome.Skipped;
        }

        var total = Stopwatch.StartNew();
        var stage = Stopwatch.StartNew();

        var play = await LoadPlay(playId, contestId);

        if (play is null)
        {
            _logger.LogWarning("Play not found for instant analysis.");
            return PlayAnalysisOutcome.NotFound;
        }

        var loadMs = Lap(stage);

        var events = PlayClassifier.Classify(play);
        var classifyMs = Lap(stage);

        _logger.LogInformation(
            "Play analysed. Sequence={Sequence}, PlayTypeId={PlayTypeId}, Kinds={Kinds}, Actors={Actors}, Victims={Victims}, Roles={Roles}, LiveAnalysisEnabled={LiveAnalysisEnabled}",
            play.SequenceNumber,
            play.TypeId,
            string.Join(",", events.Select(e => e.Kind)),
            string.Join(",", events.SelectMany(e => e.Actors).Distinct()),
            string.Join(",", events.Where(e => e.Victim.HasValue).Select(e => e.Victim!.Value).Distinct()),
            string.Join(",", play.Participants.Select(p => $"{p.Type}:{p.AthleteSeasonId}")),
            true);

        // Most plays classify to nothing and have no facts; the audit query
        // still lists them, with none.
        if (events.Count == 0)
            return new PlayAnalysisOutcome(PlayAnalysisStatus.Analysed, 0, 0, []);

        var gamePlays = await LoadGamePlays(play.CompetitionId);
        var defenseByOffense = await LoadDefenseByOffense(play.CompetitionId);
        var contest = await LoadContest(play.ContestId);
        loadMs += Lap(stage);

        var gameFacts = GameFacts.Compute(play, events, gamePlays, defenseByOffense);
        var gameFactsMs = Lap(stage);

        var athletes = events.SelectMany(e => e.Actors)
            .Concat(events.Where(e => e.Victim.HasValue).Select(e => e.Victim!.Value))
            .Distinct()
            .ToList();
        DateTime? cutoffUtc = documentRule == SeasonDocumentRule.BeforeKickoff ? contest.StartDateUtc : null;
        var seasonStats = await LoadSeasonStats(athletes, cutoffUtc);
        var seasonFacts = SeasonFacts.Compute(play, events, gamePlays, seasonStats, contest.StartDateUtc, cutoffUtc);
        var seasonFactsMs = Lap(stage);

        var facts = gameFacts.Facts.Concat(seasonFacts.Facts).ToList();
        var offense = play.StartFranchiseSeasonId;
        Guid? defense = offense is { } o && defenseByOffense.TryGetValue(o, out var d) ? d : null;

        var teams = new TeamFreshness(offense, defense, await LoadPreviousGameFinals([offense, defense], contest));
        var names = await LoadNames(facts);
        var selection = FactSelector.Select(facts, teams, names);
        var selectMs = Lap(stage);

        var persisted = await Persist(play, selection, analysisRunId, correlationId, contest);
        var persistMs = Lap(stage);

        foreach (var drop in gameFacts.Dropped.Concat(seasonFacts.Dropped).Concat(selection.Dropped))
        {
            _logger.LogInformation(
                "Fact dropped. FactType={FactType}, Kind={Kind}, AthleteSeasonId={AthleteSeasonId}, Reason={Reason}",
                drop.FactType, drop.Kind, drop.AthleteSeasonId, drop.Reason);
        }

        foreach (var selected in selection.Selected)
        {
            _logger.LogInformation(
                "Fact generated. Ordinal={Ordinal}, FactType={FactType}, Kind={Kind}, Scope={Scope}, Value={Value}, Priority={Priority}, Text={Text}, AthleteSeasonId={AthleteSeasonId}, FranchiseSeasonId={FranchiseSeasonId}, Provenance={Provenance}",
                selected.Ordinal,
                selected.Fact.FactType,
                selected.Fact.Kind,
                selected.Fact.FactType.StartsWith("Season", StringComparison.Ordinal) ? "season" : "game",
                selected.Fact.Value,
                selected.Priority,
                selected.Text,
                selected.Fact.AthleteSeasonId,
                selected.Fact.FranchiseSeasonId,
                JsonSerializer.Serialize(selected.Fact.Provenance, JsonOptions));
        }

        _logger.LogInformation(
            "Play analysis timing. LoadMs={LoadMs}, ClassifyMs={ClassifyMs}, GameFactsMs={GameFactsMs}, SeasonFactsMs={SeasonFactsMs}, SelectMs={SelectMs}, PersistMs={PersistMs}, TotalMs={TotalMs}, Generated={Generated}, Persisted={Persisted}, Dropped={Dropped}",
            loadMs, classifyMs, gameFactsMs, seasonFactsMs, selectMs, persistMs, total.Elapsed.TotalMilliseconds,
            selection.Selected.Count, persisted,
            gameFacts.Dropped.Count + seasonFacts.Dropped.Count + selection.Dropped.Count);

        return new PlayAnalysisOutcome(
            PlayAnalysisStatus.Analysed,
            selection.Selected.Count,
            persisted,
            [.. gameFacts.Dropped, .. seasonFacts.Dropped, .. selection.Dropped]);
    }

    private static double Lap(Stopwatch stage)
    {
        var ms = stage.Elapsed.TotalMilliseconds;
        stage.Restart();
        return ms;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Writes the play's fact set in one save and, for live analysis only,
    /// publishes <see cref="PlayAnalysisGenerated"/> through the outbox in the
    /// same save (task 10), so the event goes out if and only if the rows
    /// commit. The unique index on (PlayId, AnalysisRunId, Ordinal) makes a
    /// racing duplicate job fail as a whole and leave the first set (and its
    /// event); that's success, not an error. Lab runs never publish.
    /// </summary>
    private async Task<int> Persist(
        AnalysedPlay play,
        FactSelection selection,
        Guid? analysisRunId,
        Guid correlationId,
        ContestInfo contest)
    {
        if (selection.Selected.Count == 0)
            return 0;

        var now = _dateTimeProvider.UtcNow();

        var rows = selection.Selected.Select(s => new PlayAnalysis
        {
            ContestId = play.ContestId,
            CompetitionId = play.CompetitionId,
            PlayId = play.PlayId,
            AnalysisRunId = analysisRunId,
            Ordinal = s.Ordinal,
            FactType = s.Fact.FactType,
            Facts = JsonSerializer.Serialize(new
            {
                kind = s.Fact.Kind.ToString(),
                athleteSeasonId = s.Fact.AthleteSeasonId,
                franchiseSeasonId = s.Fact.FranchiseSeasonId,
                value = s.Fact.Value,
                provenance = s.Fact.Provenance
            }, JsonOptions),
            Text = s.Text,
            Priority = s.Priority,
            TemplateVersion = s.TemplateVersion,
            CreatedUtc = now,
            CreatedBy = correlationId
        }).ToList();

        _dataContext.PlayAnalyses.AddRange(rows);

        if (analysisRunId is null)
        {
            await _eventBus.Publish(new PlayAnalysisGenerated(
                play.ContestId,
                play.CompetitionId,
                play.PlayId,
                rows.Select(r => new PlayAnalysisItem(r.Id, r.FactType, r.Text)).ToList(),
                null,
                contest.Sport,
                contest.SeasonYear,
                correlationId,
                CausationId.Producer.PlayAnalyser));
        }

        try
        {
            await _dataContext.SaveChangesAsync();
            return rows.Count;
        }
        catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
        {
            _logger.LogInformation("Play's fact set was already written by another job; keeping the first.");

            foreach (var row in rows)
                _dataContext.Entry(row).State = EntityState.Detached;

            // The event was staged as an outbox row in the same save; drop it
            // too, so nothing else on this context can send it later.
            foreach (var outboxEntry in _dataContext.ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added && e.Entity is OutboxMessage)
                         .ToList())
            {
                outboxEntry.State = EntityState.Detached;
            }

            return 0;
        }
    }

    private record ContestInfo(DateTime StartDateUtc, int SeasonYear, Sport Sport);

    private async Task<ContestInfo> LoadContest(Guid contestId) =>
        await _dataContext.Contests
            .AsNoTracking()
            .Where(x => x.Id == contestId)
            .Select(x => new ContestInfo(x.StartDateUtc, x.SeasonYear, x.Sport))
            .FirstOrDefaultAsync()
        ?? new ContestInfo(default, default, default);

    /// <summary>Per team, when its previous game this season was finalized (null: not yet); no entry: no previous game.</summary>
    private async Task<IReadOnlyDictionary<Guid, DateTime?>> LoadPreviousGameFinals(
        IEnumerable<Guid?> teams,
        ContestInfo contest)
    {
        var result = new Dictionary<Guid, DateTime?>();

        foreach (var team in teams.OfType<Guid>().Distinct())
        {
            var previous = await _dataContext.Contests
                .AsNoTracking()
                .Where(x => x.SeasonYear == contest.SeasonYear &&
                            x.StartDateUtc < contest.StartDateUtc &&
                            (x.HomeTeamFranchiseSeasonId == team || x.AwayTeamFranchiseSeasonId == team))
                .OrderByDescending(x => x.StartDateUtc)
                .Select(x => new { x.FinalizedUtc })
                .FirstOrDefaultAsync();

            if (previous is not null)
                result[team] = previous.FinalizedUtc;
        }

        return result;
    }

    private async Task<AnalysisNames> LoadNames(IReadOnlyCollection<PlayFact> facts)
    {
        var athleteIds = facts.Where(f => f.AthleteSeasonId.HasValue).Select(f => f.AthleteSeasonId!.Value).Distinct().ToList();
        var teamIds = facts.Where(f => f.FranchiseSeasonId.HasValue).Select(f => f.FranchiseSeasonId!.Value).Distinct().ToList();

        var athletes = athleteIds.Count == 0
            ? []
            : await _dataContext.AthleteSeasons
                .AsNoTracking()
                .Where(x => athleteIds.Contains(x.Id) && x.ShortName != null && x.ShortName != "")
                .Select(x => new { x.Id, x.ShortName })
                .ToListAsync();

        var teams = teamIds.Count == 0
            ? []
            : await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Where(x => teamIds.Contains(x.Id))
                .Select(x => new { x.Id, x.DisplayNameShort })
                .ToListAsync();

        return new AnalysisNames(
            athletes.ToDictionary(x => x.Id, x => x.ShortName!),
            teams.ToDictionary(x => x.Id, x => x.DisplayNameShort));
    }

    private async Task<IReadOnlyDictionary<Guid, SeasonStatLine>> LoadSeasonStats(
        IReadOnlyCollection<Guid> athleteSeasonIds,
        DateTime? documentCutoffUtc)
    {
        if (athleteSeasonIds.Count == 0)
            return new Dictionary<Guid, SeasonStatLine>();

        var keys = SeasonFacts.StatKeys;
        var categories = keys.Select(k => k.Split('.')[0]).Distinct().ToList();
        var stats = keys.Select(k => k.Split('.')[1]).Distinct().ToList();

        var rows = await _dataContext.AthleteSeasonStatistics
            .AsNoTracking()
            .Where(x => athleteSeasonIds.Contains(x.AthleteSeasonId) && x.SplitId == "0")
            .Select(x => new
            {
                x.AthleteSeasonId,
                x.Id,
                x.CreatedUtc,
                Stats = x.Categories
                    .Where(c => categories.Contains(c.Name))
                    .SelectMany(c => c.Stats
                        .Where(st => stats.Contains(st.Name) && st.Value != null)
                        .Select(st => new { Category = c.Name, st.Name, st.Value }))
                    .ToList()
            })
            .ToListAsync();

        var lines = rows.Select(x => new SeasonStatLine(
            x.AthleteSeasonId,
            x.Id,
            x.CreatedUtc,
            x.Stats
                .Select(st => (Key: $"{st.Category}.{st.Name}", st.Value))
                .Where(st => keys.Contains(st.Key))
                .GroupBy(st => st.Key)
                .ToDictionary(g => g.Key, g => g.First().Value!.Value)));

        return SeasonFacts.NewestPerAthlete(lines, documentCutoffUtc);
    }

    private async Task<IReadOnlyList<GamePlay>> LoadGamePlays(Guid competitionId)
    {
        var rows = await _dataContext.CompetitionPlays
            .AsNoTracking()
            .Where(x => x.CompetitionId == competitionId)
            .Select(x => new
            {
                x.Id,
                x.SequenceNumber,
                x.TypeId,
                x.StatYardage,
                x.StartFranchiseSeasonId,
                Participants = x.Participants
                    .Select(p => new AnalysedPlayParticipant(p.AthleteSeasonId, p.Type, p.Order))
                    .ToList()
            })
            .ToListAsync();

        // SequenceNumber is stored as text; every 2026 value is numeric.
        return rows
            .Where(x => long.TryParse(x.SequenceNumber, out _))
            .Select(x => new GamePlay(
                x.Id,
                long.Parse(x.SequenceNumber),
                x.TypeId,
                x.StatYardage,
                x.StartFranchiseSeasonId,
                x.Participants))
            .ToList();
    }

    private async Task<IReadOnlyDictionary<Guid, Guid>> LoadDefenseByOffense(Guid competitionId)
    {
        var teams = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .Where(x => x.CompetitionId == competitionId)
            .Select(x => x.FranchiseSeasonId)
            .Distinct()
            .ToListAsync();

        return teams.Count == 2
            ? new Dictionary<Guid, Guid> { [teams[0]] = teams[1], [teams[1]] = teams[0] }
            : new Dictionary<Guid, Guid>();
    }

    private Task<AnalysedPlay?> LoadPlay(Guid playId, Guid contestId) =>
        _dataContext.CompetitionPlays
            .AsNoTracking()
            .Where(x => x.Id == playId)
            .Select(x => new AnalysedPlay(
                x.Id,
                contestId,
                x.CompetitionId,
                x.SequenceNumber,
                x.Type,
                x.TypeId,
                x.Text,
                x.StatYardage,
                x.StartFranchiseSeasonId,
                x.Participants
                    .OrderBy(p => p.Order)
                    .Select(p => new AnalysedPlayParticipant(p.AthleteSeasonId, p.Type, p.Order))
                    .ToList()))
            .FirstOrDefaultAsync();
}
