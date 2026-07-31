using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Season;

namespace SportsData.Api.Application.PickemGroups;

public interface ILeagueJoinExpiryCalculator
{
    /// <summary>
    /// Recomputes and stores <c>PickemGroup.InvitationsExpireUtc</c> for one
    /// league. Idempotent recompute-from-scratch — safe to call from any
    /// trigger point, in any order, any number of times.
    /// </summary>
    Task RecomputeAsync(Guid groupId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Single authority for "when does this league stop accepting members?" —
/// the stored answer the join gate compares and the browse countdown renders.
/// See docs/features/league-join-policy-and-discovery.md, "v2 revision".
///
/// Called from three trigger points, all converging on the same computation:
///   1. End of each per-week matchup scheduling run (slates build
///      progressively — full-season leagues bootstrap the current week only,
///      so precision improves as weeks land).
///   2. <see cref="Events.ContestStartTimeUpdatedHandler"/> — kickoff times
///      move after slates generate; the stored value must follow.
///   3. <see cref="Jobs.LeagueJoinExpiryAuditJob"/> — self-healing sweep,
///      which doubles as the backfill for leagues created before this field.
///
/// The rules (operator-approved, 2026-07-30):
///   FullSeason + drop weeks  → first kickoff of week N+1 (N = drop count):
///                              the exact moment joining starts costing
///                              points. Falls back to the season calendar's
///                              week-(N+1) start until that week's matchups
///                              exist.
///   CloseAtFirstGame         → first in-window game start.
///   Open                     → the league's LAST pickable moment — authored
///                              EndsOn when present, else the season calendar
///                              end. "Open" no longer means forever; the old
///                              anchor (DeactivatedUtc) is a UI-declutter
///                              sweep that lags 7 days and never fires for
///                              full-season leagues.
/// </summary>
public class LeagueJoinExpiryCalculator : ILeagueJoinExpiryCalculator
{
    private readonly ILogger<LeagueJoinExpiryCalculator> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ISeasonClientFactory _seasonClientFactory;

    public LeagueJoinExpiryCalculator(
        ILogger<LeagueJoinExpiryCalculator> logger,
        AppDataContext dataContext,
        ISeasonClientFactory seasonClientFactory)
    {
        _logger = logger;
        _dataContext = dataContext;
        _seasonClientFactory = seasonClientFactory;
    }

    public async Task RecomputeAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _dataContext.PickemGroups
            .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken);

        if (group is null)
        {
            _logger.LogWarning("Join-expiry recompute requested for unknown group {GroupId}.", groupId);
            return;
        }

        // A deactivated league is already off every joinable surface and the
        // join gate rejects it outright — recomputing would only churn rows.
        if (group.DeactivatedUtc is not null)
            return;

        var expiry = await ComputeAsync(group, cancellationToken);

        // null means "not yet knowable" (e.g. slate not built, season calendar
        // unavailable). Never overwrite a known value with unknown — a later
        // trigger with better data wins, a worse one does not.
        if (expiry is null || expiry == group.InvitationsExpireUtc)
            return;

        _logger.LogInformation(
            "InvitationsExpireUtc for league {GroupId} ({Policy}, dropWeeks={DropWeeks}): {Old} -> {New}",
            group.Id, group.JoinPolicy, group.DropLowWeeksCount,
            group.InvitationsExpireUtc, expiry);

        group.InvitationsExpireUtc = expiry;
        await _dataContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<DateTime?> ComputeAsync(
        Infrastructure.Data.Entities.PickemGroup group,
        CancellationToken cancellationToken)
    {
        // Explicit capture, not null-inference — see the LeagueWindow enum
        // doc for why the columns alone cannot distinguish window shapes
        // once WeekRange ships.
        var isFullSeason = group.LeagueWindow == LeagueWindow.FullSeason;

        // FullSeason + drop weeks: the operator-approved DEFAULT for such
        // leagues — a joiner inside the dropped-week window pays zero
        // competitive penalty, so the window stays open through it regardless
        // of the commissioner's Open/CloseAtFirstGame choice.
        if (isFullSeason && group.DropLowWeeksCount is > 0)
        {
            var targetWeek = group.DropLowWeeksCount.Value + 1;

            // Precise once week N+1's matchups exist (the weekly scheduler
            // builds them during week N, ahead of this moment arriving).
            var firstKickoff = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.GroupId == group.Id && m.SeasonWeek == targetWeek)
                .Select(m => (DateTime?)m.StartDateUtc)
                .MinAsync(cancellationToken);

            if (firstKickoff is not null)
                return firstKickoff;

            // Provisional: the season calendar's week-(N+1) boundary.
            var overview = await GetSeasonOverviewAsync(group, cancellationToken);
            return overview?.Weeks
                // Regular-season weeks only — postseason numbering restarts.
                .Where(w => !w.SeasonPhaseName.Contains("post", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault(w => w.Number == targetWeek)?.StartDate;
        }

        if (group.JoinPolicy == JoinPolicy.CloseAtFirstGame)
        {
            // First in-window game start. Null while the slate is unbuilt —
            // nothing has started, the league is open, and a later trigger
            // fills this in.
            return await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.GroupId == group.Id)
                .Select(m => (DateTime?)m.StartDateUtc)
                .MinAsync(cancellationToken);
        }

        // Open: joinable while anything remains pickable.
        // Windowed leagues (DateRange, and WeekRange once its week-to-date
        // translation ships) have an AUTHORED end — stable, no derivation.
        if (!isFullSeason && group.EndsOn is not null)
            return group.EndsOn;

        // Open + FullSeason: the season's end from the calendar. Matchups
        // can't answer this — slates build progressively, so max(matchup)
        // would close the league between week N's last game and week N+1's
        // build (the trap documented in the v1 design).
        var seasonOverview = await GetSeasonOverviewAsync(group, cancellationToken);
        return seasonOverview?.EndDate;
    }

    private async Task<Core.Dtos.Canonical.SeasonOverviewDto?> GetSeasonOverviewAsync(
        Infrastructure.Data.Entities.PickemGroup group,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _seasonClientFactory.Resolve(group.Sport);
            var result = await client.GetSeasonOverview(group.SeasonYear, cancellationToken);
            if (result.IsSuccess)
                return result.Value;

            _logger.LogWarning(
                "Season overview unavailable for {Sport} {SeasonYear}; league {GroupId} expiry stays unset until the next trigger.",
                group.Sport, group.SeasonYear, group.Id);
            return null;
        }
        catch (Exception ex)
        {
            // Producer being briefly unreachable must not fail slate builds or
            // event consumers that call through here — the sweep self-heals.
            _logger.LogWarning(ex,
                "Season overview call failed for {Sport} {SeasonYear}; league {GroupId} expiry stays unset until the next trigger.",
                group.Sport, group.SeasonYear, group.Id);
            return null;
        }
    }
}
