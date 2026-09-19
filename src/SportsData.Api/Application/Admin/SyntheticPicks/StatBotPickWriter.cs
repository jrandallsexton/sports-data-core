using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.SyntheticPicks;

public class StatBotPickWriter : IStatBotPickWriter
{
    private readonly AppDataContext _dataContext;
    private readonly IDateTimeProvider _clock;
    private readonly ILogger<StatBotPickWriter> _logger;

    public StatBotPickWriter(
        AppDataContext dataContext,
        IDateTimeProvider clock,
        ILogger<StatBotPickWriter> logger)
    {
        _dataContext = dataContext;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> UpsertForContestAsync(Guid contestId, CancellationToken cancellationToken = default)
    {
        var preview = await LatestPreviewAsync(contestId, cancellationToken);
        if (preview is null) return 0;

        var slots = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.ContestId == contestId)
            .Join(_dataContext.PickemGroups.AsNoTracking(),
                m => m.GroupId, g => g.Id,
                (m, g) => new Slot(g.Id, g.PickType, m.SeasonWeek, m.StartDateUtc, m.HomeSpread != null))
            .ToListAsync(cancellationToken);
        if (slots.Count == 0) return 0;

        var now = _clock.UtcNow();
        var existing = await _dataContext.UserPicks
            .Where(p => p.UserId == IStatBotPickWriter.StatBotUserId && p.ContestId == contestId)
            .ToDictionaryAsync(p => p.PickemGroupId, cancellationToken);

        var written = 0;
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
                await _dataContext.UserPicks.AddAsync(NewPick(slot, contestId, w, preview.CreatedUtc), cancellationToken);
            }
            written++;
        }

        if (written > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "StatBot pick written for contest {ContestId} in {Count} league(s) from preview {PreviewId}.",
                contestId, written, preview.Id);
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
                (m, g) => new { Slot = new Slot(g.Id, g.PickType, m.SeasonWeek, m.StartDateUtc, m.HomeSpread != null), m.ContestId })
            .ToListAsync(cancellationToken);
        if (slots.Count == 0) return 0;

        var contestIds = slots.Select(s => s.ContestId).Distinct().ToList();

        var existing = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == IStatBotPickWriter.StatBotUserId && contestIds.Contains(p.ContestId))
            .Select(p => new { p.PickemGroupId, p.ContestId })
            .ToListAsync(cancellationToken);
        var have = existing.Select(e => (e.PickemGroupId, e.ContestId)).ToHashSet();

        var previews = new Dictionary<Guid, MatchupPreview?>();
        var inserted = 0;
        foreach (var s in slots)
        {
            if (have.Contains((s.Slot.GroupId, s.ContestId))) continue;

            if (!previews.TryGetValue(s.ContestId, out var preview))
            {
                preview = await LatestPreviewAsync(s.ContestId, cancellationToken);
                previews[s.ContestId] = preview;
            }
            if (preview is null) continue;

            // Backfill may fill a finished game, but only with a preview that
            // existed before kickoff; otherwise the "pick" was never makeable.
            if (preview.CreatedUtc >= s.Slot.StartDateUtc) continue;

            if (WinnerFor(s.Slot, preview) is not Guid w) continue;

            await _dataContext.UserPicks.AddAsync(NewPick(s.Slot, s.ContestId, w, preview.CreatedUtc), cancellationToken);
            inserted++;
        }

        if (inserted > 0)
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("StatBot backfill: inserted {Count} pick(s) for {SeasonYear} week {Week}.", inserted, seasonYear, week);
        }
        return inserted;
    }

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

    private sealed record Slot(Guid GroupId, PickType PickType, int Week, DateTime StartDateUtc, bool HasSpread);
}
