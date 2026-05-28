using FluentAssertions;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.PickemGroups;

/// <summary>
/// Pins <see cref="PickemGroupWeekFactory"/> behavior — the central reason
/// for extracting it was two call sites (PickemGroupCreatedHandler +
/// MatchupScheduler) drifting on IsNonStandardWeek and the postseason
/// ordinal sequence. These tests are the regression net.
/// </summary>
public class PickemGroupWeekFactoryTests
{
    [Fact]
    public void CreateForCurrentWeek_RegularSeason_UsesWeekNumberAsOrdinal()
    {
        var group = NewGroup();
        var currentWeek = NewWeek(weekNumber: 7, isPostSeason: false);

        var result = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);

        result.SeasonWeek.Should().Be(7);
        result.SeasonYear.Should().Be(currentWeek.SeasonYear);
        result.SeasonWeekId.Should().Be(currentWeek.Id);
        result.GroupId.Should().Be(group.Id);
        result.IsNonStandardWeek.Should().BeFalse();
        result.AreMatchupsGenerated.Should().BeFalse();
    }

    [Fact]
    public void CreateForCurrentWeek_RegularSeason_PropagatesIsNonStandardWeek()
    {
        var group = NewGroup();
        var currentWeek = NewWeek(weekNumber: 8, isPostSeason: false, isNonStandardWeek: true);

        var result = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);

        // Bug being prevented: PickemGroupCreatedHandler used to omit this
        // field, so a non-standard regular-season week (rare but possible)
        // landed in DB with IsNonStandardWeek=false.
        result.IsNonStandardWeek.Should().BeTrue();
    }

    [Fact]
    public void CreateForCurrentWeek_Postseason_WithNoExistingWeeks_FallsBackToWeekNumber()
    {
        // Scheduler's first run for a sport that started in postseason
        // (degenerate but possible — fresh data, recurring job firing for
        // the first time). No prior weeks to sequence off; use the raw
        // WeekNumber as the ordinal.
        var group = NewGroup();
        var currentWeek = NewWeek(weekNumber: 1, isPostSeason: true);

        var result = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);

        result.SeasonWeek.Should().Be(1);
    }

    [Fact]
    public void CreateForCurrentWeek_Postseason_WithExistingWeeks_SequencesOffHighest()
    {
        // Common case: league played a full regular season (ordinals 1..18
        // for an NFL league say), now in postseason. ESPN restarts
        // WeekNumber at 1 for the postseason; ordinal must continue the
        // monotonic sequence so the league's seasonWeeks list doesn't
        // collide on Week 1.
        var group = NewGroup(existingWeekOrdinals: [1, 2, 3, 17, 18]);
        var currentWeek = NewWeek(weekNumber: 1, isPostSeason: true);

        var result = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);

        result.SeasonWeek.Should().Be(19);
    }

    [Fact]
    public void CreateForCurrentWeek_Postseason_IgnoresWeekOrderingInGroupWeeks()
    {
        // Defensive: the factory must not assume group.Weeks is sorted.
        var group = NewGroup(existingWeekOrdinals: [18, 1, 17, 3, 2]);
        var currentWeek = NewWeek(weekNumber: 2, isPostSeason: true);

        var result = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);

        result.SeasonWeek.Should().Be(19);
    }

    [Fact]
    public void CreateForCurrentWeek_AssignsNewIdEachCall()
    {
        var group = NewGroup();
        var currentWeek = NewWeek(weekNumber: 1, isPostSeason: false);

        var first = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);
        var second = PickemGroupWeekFactory.CreateForCurrentWeek(group, currentWeek);

        first.Id.Should().NotBe(Guid.Empty);
        second.Id.Should().NotBe(Guid.Empty);
        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void CreateForCurrentWeek_NullGroup_Throws()
    {
        var currentWeek = NewWeek(weekNumber: 1, isPostSeason: false);

        var act = () => PickemGroupWeekFactory.CreateForCurrentWeek(null!, currentWeek);

        act.Should().Throw<ArgumentNullException>().WithParameterName("group");
    }

    [Fact]
    public void CreateForCurrentWeek_NullCurrentWeek_Throws()
    {
        var group = NewGroup();

        var act = () => PickemGroupWeekFactory.CreateForCurrentWeek(group, null!);

        act.Should().Throw<ArgumentNullException>().WithParameterName("currentWeek");
    }

    private static PickemGroup NewGroup(int[]? existingWeekOrdinals = null)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Sport = Sport.BaseballMlb,
            League = League.MLB,
        };

        if (existingWeekOrdinals is not null)
        {
            foreach (var ordinal in existingWeekOrdinals)
            {
                group.Weeks.Add(new PickemGroupWeek
                {
                    GroupId = group.Id,
                    SeasonWeek = ordinal,
                    SeasonYear = 2026,
                    SeasonWeekId = Guid.NewGuid(),
                });
            }
        }

        return group;
    }

    private static CanonicalSeasonWeekDto NewWeek(
        int weekNumber,
        bool isPostSeason,
        bool isNonStandardWeek = false) => new()
    {
        Id = Guid.NewGuid(),
        SeasonId = Guid.NewGuid(),
        SeasonYear = 2026,
        WeekNumber = weekNumber,
        SeasonPhase = isPostSeason ? "postseason" : "regularseason",
        IsNonStandardWeek = isNonStandardWeek,
    };
}
