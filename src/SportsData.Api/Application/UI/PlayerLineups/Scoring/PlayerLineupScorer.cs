using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Athlete;

namespace SportsData.Api.Application.UI.PlayerLineups.Scoring;

public interface IPlayerLineupScorer
{
    /// <summary>
    /// Recomputes and PERSISTS points for the given slots (entities must
    /// be tracked, lineups loaded with all their slots), then refreshes
    /// each affected lineup's total. Returns the affected lineups so the
    /// caller can broadcast. Does NOT SaveChanges — the caller owns the
    /// unit of work (consumers save once, atomically with the outbox).
    /// </summary>
    Task<IReadOnlyList<PlayerLineup>> ScoreSlotsAsync(
        Sport sport,
        IReadOnlyList<PlayerLineupSlot> slots,
        bool finalize,
        CancellationToken cancellationToken);
}

/// <summary>
/// The single write-side scorer: both scoring consumers (stats-updated,
/// contest-finalized) run through here so slot points, stat lines, and
/// lineup totals can never diverge between the live and final paths.
/// The read path stays read-only — it fills nulls with a live
/// computation but never persists.
/// </summary>
public class PlayerLineupScorer : IPlayerLineupScorer
{
    private readonly ILogger<PlayerLineupScorer> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IAthleteClientFactory _athleteClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public PlayerLineupScorer(
        ILogger<PlayerLineupScorer> logger,
        AppDataContext dataContext,
        IAthleteClientFactory athleteClientFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _athleteClientFactory = athleteClientFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<IReadOnlyList<PlayerLineup>> ScoreSlotsAsync(
        Sport sport,
        IReadOnlyList<PlayerLineupSlot> slots,
        bool finalize,
        CancellationToken cancellationToken)
    {
        if (slots.Count == 0) return [];

        var rules = await _dataContext.PlayerScoringRules
            .AsNoTracking()
            .Where(r => r.RuleSet.IsDefault)
            .Select(r => new ScoringRule(r.StatKey, r.Points, r.PerUnits))
            .ToListAsync(cancellationToken);
        if (rules.Count == 0)
        {
            _logger.LogWarning("No default scoring rule set; skipping slot scoring.");
            return [];
        }

        var statlines = await _athleteClientFactory
            .Resolve(sport)
            .GetAthleteStatlines(
                slots.Where(s => s.ContestId.HasValue).Select(s => s.ContestId!.Value).Distinct().ToList(),
                slots.Select(s => s.AthleteSeasonId).Distinct().ToList(),
                cancellationToken);
        if (!statlines.IsSuccess)
        {
            _logger.LogWarning("Statline fetch failed; skipping slot scoring. Status={Status}", statlines.Status);
            return [];
        }

        var byKey = statlines.Value.ToDictionary(x => (x.AthleteSeasonId, x.ContestId));
        var now = _dateTimeProvider.UtcNow();
        var affectedLineups = new Dictionary<Guid, PlayerLineup>();

        foreach (var slot in slots)
        {
            if (!slot.ContestId.HasValue) continue;
            if (!byKey.TryGetValue((slot.AthleteSeasonId, slot.ContestId.Value), out var line))
            {
                // Finalization without a statline still freezes the slot at
                // its current (possibly null) points — the game is over;
                // nothing further will arrive.
                if (finalize) slot.IsScoreFinal = true;
                continue;
            }

            var score = PlayerPickemScoringEngine.Score(rules, line.Stats);
            slot.Points = score.Points;
            slot.StatLine = PlayerPickemScoringEngine.BuildStatLine(score.Contributions);
            if (finalize) slot.IsScoreFinal = true;
            slot.ModifiedUtc = now;

            affectedLineups.TryAdd(slot.Lineup.Id, slot.Lineup);
        }

        foreach (var lineup in affectedLineups.Values)
        {
            lineup.TotalPoints = lineup.Slots.Sum(s => s.Points ?? 0m);
            lineup.ScoreUpdatedUtc = now;
        }

        return affectedLineups.Values.ToList();
    }
}
