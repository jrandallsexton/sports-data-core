using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Dtos.Canonical;

namespace SportsData.Api.Application.PickemGroups;

/// <summary>
/// Single source of truth for building a <see cref="PickemGroupWeek"/> row.
/// Two call sites previously constructed this entity inline and had drifted —
/// <c>PickemGroupCreatedHandler</c> omitted <see cref="PickemGroupWeek.IsNonStandardWeek"/>
/// and didn't adjust the <see cref="PickemGroupWeek.SeasonWeek"/> ordinal for
/// postseason; <c>MatchupScheduler</c> did both. Centralizing here so the two
/// paths can't drift again.
/// </summary>
public static class PickemGroupWeekFactory
{
    /// <summary>
    /// Build a <see cref="PickemGroupWeek"/> for the league's current week.
    /// </summary>
    /// <param name="group">
    /// The league. <see cref="PickemGroup.Weeks"/> must be loaded — the
    /// postseason ordinal adjustment reads existing weeks to compute the
    /// next sequential <see cref="PickemGroupWeek.SeasonWeek"/> value.
    /// </param>
    /// <param name="currentWeek">Sport-resolved current season week.</param>
    public static PickemGroupWeek CreateForCurrentWeek(
        PickemGroup group,
        CanonicalSeasonWeekDto currentWeek)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(currentWeek);

        return new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            AreMatchupsGenerated = false,
            GroupId = group.Id,
            SeasonWeek = ResolveOrdinal(group, currentWeek),
            SeasonYear = currentWeek.SeasonYear,
            SeasonWeekId = currentWeek.Id,
            IsNonStandardWeek = currentWeek.IsNonStandardWeek,
        };
    }

    /// <summary>
    /// Postseason weeks restart at WeekNumber=1 on the ESPN side, which would
    /// collide with the league's existing regular-season Week 1 row. Sequence
    /// the postseason ordinal off the league's highest existing week instead
    /// so the list stays monotonically increasing (used by ascending
    /// <c>seasonWeeks</c> projections on /user/me and the week selector).
    /// Falls back to <c>currentWeek.WeekNumber</c> for a league with no prior
    /// weeks (first run of the recurring scheduler hits this).
    /// </summary>
    private static int ResolveOrdinal(PickemGroup group, CanonicalSeasonWeekDto currentWeek)
    {
        if (!currentWeek.IsPostSeason)
            return currentWeek.WeekNumber;

        var highestExisting = group.Weeks
            .OrderByDescending(x => x.SeasonWeek)
            .FirstOrDefault();

        return (highestExisting?.SeasonWeek + 1) ?? currentWeek.WeekNumber;
    }
}
