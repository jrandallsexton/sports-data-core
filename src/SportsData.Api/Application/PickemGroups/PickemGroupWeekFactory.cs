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
    /// Build a <see cref="PickemGroupWeek"/> for the given
    /// <see cref="CanonicalSeasonWeekDto"/>. Used at both creation time
    /// (any SeasonWeek the league window overlaps) and from the daily
    /// scheduler (the sport's then-current SeasonWeek).
    /// </summary>
    /// <param name="group">
    /// The league. <see cref="PickemGroup.Weeks"/> must be loaded — the
    /// postseason ordinal adjustment reads existing weeks to compute the
    /// next sequential <see cref="PickemGroupWeek.SeasonWeek"/> value.
    /// </param>
    /// <param name="week">Sport-resolved season week DTO.</param>
    public static PickemGroupWeek Create(
        PickemGroup group,
        CanonicalSeasonWeekDto week)
    {
        ArgumentNullException.ThrowIfNull(group);
        ArgumentNullException.ThrowIfNull(week);

        return new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            AreMatchupsGenerated = false,
            GroupId = group.Id,
            SeasonWeek = ResolveOrdinal(group, week),
            SeasonYear = week.SeasonYear,
            SeasonWeekId = week.Id,
            IsNonStandardWeek = week.IsNonStandardWeek,
        };
    }

    /// <summary>
    /// Postseason weeks restart at WeekNumber=1 on the ESPN side, which would
    /// collide with the league's existing regular-season Week 1 row. Sequence
    /// the postseason ordinal off the league's highest existing week instead
    /// so the list stays monotonically increasing (used by ascending
    /// <c>seasonWeeks</c> projections on /user/me and the week selector).
    /// Falls back to <c>week.WeekNumber</c> for a league with no prior
    /// weeks (first run of the recurring scheduler hits this).
    /// </summary>
    private static int ResolveOrdinal(PickemGroup group, CanonicalSeasonWeekDto week)
    {
        if (!week.IsPostSeason)
            return week.WeekNumber;

        var highestExisting = group.Weeks
            .OrderByDescending(x => x.SeasonWeek)
            .FirstOrDefault();

        return (highestExisting?.SeasonWeek + 1) ?? week.WeekNumber;
    }
}
