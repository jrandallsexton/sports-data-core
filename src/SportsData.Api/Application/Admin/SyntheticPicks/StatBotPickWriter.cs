using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin.SyntheticPicks;

public class StatBotPickWriter : IStatBotPickWriter
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _clock;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<StatBotPickWriter> _logger;

    public StatBotPickWriter(
        AppDataContext dataContext,
        IDateTimeProvider clock,
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<StatBotPickWriter> logger)
    {
        _dataContext = dataContext;
        _clock = clock;
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    public Task<int> UpsertForContestAsync(Guid contestId, CancellationToken cancellationToken = default) =>
        UpsertFromPreviewAsync(contestId, previewId: null, cancellationToken);

    public Task<int> UpsertForContestAsync(Guid contestId, Guid previewId, CancellationToken cancellationToken = default) =>
        UpsertFromPreviewAsync(contestId, previewId, cancellationToken);

    private async Task<int> UpsertFromPreviewAsync(Guid contestId, Guid? previewId, CancellationToken cancellationToken)
    {
        var preview = previewId is Guid id
            ? await NamedPreviewAsync(contestId, id, cancellationToken)
            : await LatestPreviewAsync(contestId, cancellationToken);
        if (preview is null) return 0;

        var slots = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.ContestId == contestId)
            .Join(_dataContext.PickemGroups.AsNoTracking(),
                m => m.GroupId, g => g.Id,
                (m, g) => new Slot(g.Id, g.PickType, m.SeasonWeek, m.StartDateUtc, m.HomeSpread != null, g.UseConfidencePoints, m.SeasonYear))
            .ToListAsync(cancellationToken);
        if (slots.Count == 0) return 0;

        var now = _clock.UtcNow();
        var existing = await _dataContext.UserPicks
            .Where(p => p.UserId == IStatBotPickWriter.StatBotUserId && p.ContestId == contestId)
            .ToDictionaryAsync(p => p.PickemGroupId, cancellationToken);

        var written = 0;
        var insertedPickIds = new HashSet<Guid>();
        foreach (var slot in slots)
        {
            // A pick after kickoff is not a pick. Neither inserted nor changed.
            if (slot.StartDateUtc <= now) continue;

            var winner = WinnerFor(slot, preview);
            if (winner is not Guid w) continue;

            if (existing.TryGetValue(slot.GroupId, out var pick))
            {
                if (pick.FranchiseSeasonId == w) continue;
                pick.FranchiseSeasonId = w;
                pick.ModifiedUtc = now;
                pick.ModifiedBy = IStatBotPickWriter.StatBotUserId;
            }
            else
            {
                var inserted = NewPick(slot, contestId, w, preview.CreatedUtc);
                await _dataContext.UserPicks.AddAsync(inserted, cancellationToken);
                insertedPickIds.Add(inserted.Id);
            }
            written++;
        }

        if (written > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "StatBot pick written for contest {ContestId} in {Count} league(s) from preview {PreviewId}.",
                contestId, written, preview.Id);

            // Confidence is a RANKING across the week, not a per-pick value, so
            // a single new pick renumbers the rest. Cheap here: one league-week
            // at a time, and only leagues that use it.
            foreach (var slot in slots.Where(x => x.UseConfidencePoints).DistinctBy(x => x.GroupId))
                await ReconcileConfidenceAsync(slot.GroupId, slot.SeasonYear, slot.Week, insertedPickIds, cancellationToken);
        }
        return written;
    }

    public async Task<int> BackfillWeekAsync(int seasonYear, int week, CancellationToken cancellationToken = default)
    {
        var slots = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.SeasonYear == seasonYear && m.SeasonWeek == week)
            .Join(_dataContext.PickemGroups.AsNoTracking(),
                m => m.GroupId, g => g.Id,
                (m, g) => new { Slot = new Slot(g.Id, g.PickType, m.SeasonWeek, m.StartDateUtc, m.HomeSpread != null, g.UseConfidencePoints, m.SeasonYear), m.ContestId })
            .ToListAsync(cancellationToken);
        if (slots.Count == 0) return 0;

        var contestIds = slots.Select(s => s.ContestId).Distinct().ToList();

        var existing = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == IStatBotPickWriter.StatBotUserId && contestIds.Contains(p.ContestId))
            .Select(p => new { p.PickemGroupId, p.ContestId })
            .ToListAsync(cancellationToken);
        var have = existing.Select(e => (e.PickemGroupId, e.ContestId)).ToHashSet();

        var previews = new Dictionary<(Guid ContestId, DateTime Kickoff), MatchupPreview?>();
        var touchedContestIds = new HashSet<Guid>();
        var insertedPickIds = new HashSet<Guid>();
        var inserted = 0;
        foreach (var s in slots)
        {
            if (have.Contains((s.Slot.GroupId, s.ContestId))) continue;

            // Newest preview that PREDATES kickoff, not "newest, then reject if
            // late". Preview generation can run after kickoff but before the
            // contest is marked complete, and taking the newest would then skip
            // a contest that DID have an eligible earlier preview.
            var key = (s.ContestId, s.Slot.StartDateUtc);
            if (!previews.TryGetValue(key, out var preview))
            {
                preview = await LatestPreviewBeforeAsync(s.ContestId, s.Slot.StartDateUtc, cancellationToken);
                previews[key] = preview;
            }
            if (preview is null) continue;

            if (WinnerFor(s.Slot, preview) is not Guid w) continue;

            var newPick = NewPick(s.Slot, s.ContestId, w, preview.CreatedUtc);
            await _dataContext.UserPicks.AddAsync(newPick, cancellationToken);
            insertedPickIds.Add(newPick.Id);
            touchedContestIds.Add(s.ContestId);
            inserted++;
        }

        if (inserted > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StatBot backfill: inserted {Count} pick(s) for {SeasonYear} week {Week}.", inserted, seasonYear, week);

            foreach (var g in slots.Where(x => x.Slot.UseConfidencePoints).Select(x => x.Slot).DistinctBy(x => x.GroupId))
                await ReconcileConfidenceAsync(g.GroupId, seasonYear, week, insertedPickIds, cancellationToken);

            // Picks are scored by ContestFinalized, which already fired for a
            // week being backfilled — so these rows would sit unscored forever.
            // PickScoringProcessor short-circuits on "no unscored picks", and
            // these ARE unscored, so it proceeds rather than skipping.
            foreach (var contestId in touchedContestIds)
                _backgroundJobProvider.Enqueue<IScorePicks>(p => p.Process(new ScorePicksCommand(contestId)));

            _logger.LogInformation(
                "StatBot backfill: enqueued scoring for {Count} contest(s).", touchedContestIds.Count);
        }
        return inserted;
    }

    /// <summary>
    /// Assigns StatBot's confidence points across one league-week: most points
    /// to the game the model was most sure of.
    /// </summary>
    /// <remarks>
    /// Confidence leagues score a pick by its assigned confidence
    /// (PickScoringService: <c>ConfidencePoints ?? 0</c>), so a null leaves
    /// StatBot scoring ZERO on every correct pick — last place by construction,
    /// presented as a real standing. SubmitPickCommandHandler already refuses a
    /// pick with no confidence in these leagues; this writer bypasses that path,
    /// so the invariant has to be kept here.
    /// <para>
    /// Ranked by the preview's own predicted margin. Slate order was the
    /// alternative and is strictly worse: it stakes the most points on whichever
    /// game happens to kick off first, which is noise that reads like signal.
    /// The margin is what the model actually believed.
    /// </para>
    /// <para>
    /// Points run N..1 over the picks StatBot holds in that league-week, matching
    /// the pick sheet contract ("Points run 1..totalGames", each distinct).
    /// Kickoff time breaks ties so the assignment is deterministic and a re-run
    /// writes nothing.
    /// </para>
    /// </remarks>
    private async Task ReconcileConfidenceAsync(
        Guid groupId,
        int seasonYear,
        int week,
        IReadOnlySet<Guid> newlyInsertedPickIds,
        CancellationToken ct)
    {
        // Two reads rather than a join: the picks must come back TRACKED so the
        // assignment below persists, and mixing a tracked set with an untracked
        // one in a single projection does not reliably give that.
        var weekContestIds = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == groupId && m.SeasonYear == seasonYear && m.SeasonWeek == week)
            .Select(m => new { m.ContestId, m.StartDateUtc })
            .ToListAsync(ct);

        if (weekContestIds.Count == 0) return;

        var kickoffByContest = weekContestIds
            .GroupBy(x => x.ContestId)
            .ToDictionary(g => g.Key, g => g.Min(x => x.StartDateUtc));

        var contestIds = kickoffByContest.Keys.ToList();

        var picks = await _dataContext.UserPicks
            .Where(p => p.UserId == IStatBotPickWriter.StatBotUserId
                        && p.PickemGroupId == groupId
                        && contestIds.Contains(p.ContestId))
            .ToListAsync(ct);

        if (picks.Count == 0) return;

        var previews = await _dataContext.MatchupPreviews
            .AsNoTracking()
            .Where(pv => pv.RejectedUtc == null
                         && pv.AwayScore != null
                         && pv.HomeScore != null
                         && contestIds.Contains(pv.ContestId))
            .Select(pv => new { pv.ContestId, pv.AwayScore, pv.HomeScore, pv.CreatedUtc })
            .ToListAsync(ct);

        var marginByContest = previews
            .GroupBy(x => x.ContestId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.CreatedUtc)
                      .Select(x => Math.Abs(x.AwayScore!.Value - x.HomeScore!.Value))
                      .First());

        // Locked: already scored, or its kickoff has passed. PickScoringService
        // has already turned ConfidencePoints into PointsAwarded for those, and
        // PickScoringProcessor only ever re-scores the contest it is handed —
        // so renumbering a scored pick silently desyncs the points a member can
        // see from the points StatBot was actually awarded.
        //
        // Newly inserted picks are exempt: a backfill fills a week whose games
        // have all kicked off, and those rows still need a value.
        var now = _clock.UtcNow();
        var locked = picks
            .Where(p => !newlyInsertedPickIds.Contains(p.Id)
                        && (p.ScoredAt != null
                            || (kickoffByContest.TryGetValue(p.ContestId, out var k) && k <= now)))
            .ToList();

        var reserved = locked
            .Where(p => p.ConfidencePoints.HasValue)
            .Select(p => p.ConfidencePoints!.Value)
            .ToHashSet();

        var mutable = picks.Except(locked).ToList();
        if (mutable.Count == 0) return;

        // Points still run 1..N across the whole week, with the locked ones
        // holding their existing values — so the set stays distinct and the
        // pick sheet contract survives a partial renumber.
        var available = Enumerable.Range(1, picks.Count)
            .Where(v => !reserved.Contains(v))
            .OrderByDescending(v => v)
            .ToList();

        // A preview with no predicted score sorts last rather than dropping the
        // pick: every pick in a confidence league must carry a value.
        var ranked = mutable
            .OrderByDescending(p => marginByContest.TryGetValue(p.ContestId, out var m) ? m : -1)
            .ThenBy(p => kickoffByContest.TryGetValue(p.ContestId, out var k) ? k : DateTime.MaxValue)
            .ThenBy(p => p.ContestId)
            .ToList();

        var changed = 0;
        for (var i = 0; i < ranked.Count && i < available.Count; i++)
        {
            var pick = ranked[i];
            if (pick.ConfidencePoints == available[i]) continue;

            pick.ConfidencePoints = available[i];
            pick.ModifiedUtc = now;
            pick.ModifiedBy = IStatBotPickWriter.StatBotUserId;
            changed++;
        }

        if (changed > 0)
        {
            await _dataContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "StatBot confidence reconciled. GroupId={GroupId}, Week={Week}, Picks={Picks}, Locked={Locked}, Changed={Changed}",
                groupId, week, picks.Count, locked.Count, changed);
        }
    }

    private Task<MatchupPreview?> NamedPreviewAsync(Guid contestId, Guid previewId, CancellationToken ct) =>
        _dataContext.MatchupPreviews
            .AsNoTracking()
            .Where(p => p.Id == previewId && p.ContestId == contestId && p.RejectedUtc == null)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Newest non-rejected preview created strictly BEFORE a kickoff. The
    /// backfill contract is that StatBot's record is made of picks that were
    /// makeable at the time.
    /// </summary>
    private Task<MatchupPreview?> LatestPreviewBeforeAsync(Guid contestId, DateTime kickoffUtc, CancellationToken ct) =>
        _dataContext.MatchupPreviews
            .AsNoTracking()
            .Where(p => p.ContestId == contestId && p.RejectedUtc == null && p.CreatedUtc < kickoffUtc)
            .OrderByDescending(p => p.CreatedUtc)
            .FirstOrDefaultAsync(ct);

    private Task<MatchupPreview?> LatestPreviewAsync(Guid contestId, CancellationToken ct) =>
        _dataContext.MatchupPreviews
            .AsNoTracking()
            .Where(p => p.ContestId == contestId && p.RejectedUtc == null)
            .OrderByDescending(p => p.CreatedUtc)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// ATS leagues take the spread winner when the matchup has a line and the
    /// preview named one; everything else is the straight-up winner. An
    /// empty Guid is "no prediction", never a pick.
    /// </summary>
    private static Guid? WinnerFor(Slot slot, MatchupPreview preview)
    {
        Guid? Clean(Guid? g) => g is null || g == Guid.Empty ? null : g;

        if (slot.PickType == PickType.AgainstTheSpread && slot.HasSpread)
            return Clean(preview.PredictedSpreadWinner) ?? Clean(preview.PredictedStraightUpWinner);

        return Clean(preview.PredictedStraightUpWinner);
    }

    private static PickemGroupUserPick NewPick(Slot slot, Guid contestId, Guid winner, DateTime previewCreatedUtc) => new()
    {
        UserId = IStatBotPickWriter.StatBotUserId,
        PickemGroupId = slot.GroupId,
        ContestId = contestId,
        Week = slot.Week,
        FranchiseSeasonId = winner,
        PickType = slot.PickType == PickType.StraightUp ? PickType.StraightUp : PickType.AgainstTheSpread,
        TiebreakerType = TiebreakerType.TotalPoints,
        // Dated when the preview was written: that is when the AI decided.
        CreatedUtc = previewCreatedUtc,
        CreatedBy = IStatBotPickWriter.StatBotUserId
    };

    private sealed record Slot(Guid GroupId, PickType PickType, int Week, DateTime StartDateUtc, bool HasSpread, bool UseConfidencePoints, int SeasonYear);
}
