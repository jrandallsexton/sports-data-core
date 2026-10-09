using FluentAssertions;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetLeagueWeekLineups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.PlayerLineups;

/// <summary>
/// Every member's lineup for one league week: the caller sees their whole
/// lineup, other members' slots appear only once their game has locked
/// (kickoff-5), members without a lineup still appear, and rows order by
/// points.
/// </summary>
public class GetLeagueWeekLineupsQueryHandlerTests : ApiTestBase<GetLeagueWeekLineupsQueryHandler>
{
    private static readonly Guid LeagueId = Guid.NewGuid();
    private static readonly DateTime FixedNow = new(2026, 10, 3, 18, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();
    private static readonly Guid Carol = Guid.NewGuid();

    public GetLeagueWeekLineupsQueryHandlerTests()
    {
        Mocker.Use<FluentValidation.IValidator<GetLeagueWeekLineupsQuery>>(
            new GetLeagueWeekLineupsQueryValidator());
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
    }

    private async Task SeedAsync()
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = LeagueId,
            Name = "PP",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Alice,
            SeasonYear = 2026,
            GroupType = GroupType.PlayerPickem,
        });
        foreach (var (id, name) in new[] { (Alice, "Alice"), (Bob, "Bob"), (Carol, "Carol") })
        {
            DataContext.Users.Add(new UserEntity
            {
                Id = id,
                FirebaseUid = $"fb-{id:N}",
                Email = $"{name}@x.com",
                SignInProvider = "password",
                DisplayName = name,
                Username = name.ToLowerInvariant(),
            });
            DataContext.PickemGroupMembers.Add(new PickemGroupMember
            {
                Id = Guid.NewGuid(),
                PickemGroupId = LeagueId,
                UserId = id,
                Role = LeagueRole.Member,
                CreatedBy = id,
            });
        }

        // Alice and Bob each have one started game (locked) and one future
        // game (unlocked). Carol has no lineup.
        AddLineup(Alice, total: 12m);
        AddLineup(Bob, total: 20m);
        await DataContext.SaveChangesAsync();
    }

    private void AddLineup(Guid userId, decimal total)
    {
        var lineup = new PlayerLineup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = LeagueId,
            UserId = userId,
            SeasonYear = 2026,
            SeasonWeek = 5,
            TotalPoints = total,
            CreatedUtc = FixedNow,
            CreatedBy = userId,
        };
        lineup.Slots.Add(Slot(lineup.Id, userId, "QB", FixedNow.AddHours(-2), total));
        lineup.Slots.Add(Slot(lineup.Id, userId, "K", FixedNow.AddDays(1), null));
        DataContext.PlayerLineups.Add(lineup);
    }

    private static PlayerLineupSlot Slot(Guid lineupId, Guid userId, string slotId, DateTime start, decimal? points) => new()
    {
        Id = Guid.NewGuid(),
        PlayerLineupId = lineupId,
        SlotId = slotId,
        AthleteId = Guid.NewGuid(),
        AthleteSeasonId = Guid.NewGuid(),
        Position = slotId,
        FirstName = "F",
        LastName = slotId,
        TeamName = "T",
        TeamSlug = "t",
        ContestId = Guid.NewGuid(),
        ContestStartUtc = start,
        Points = points,
        CreatedUtc = FixedNow,
        CreatedBy = userId,
    };

    [Fact]
    public async Task CallerSeesTheirWholeLineup_OthersOnlyLockedSlots()
    {
        await SeedAsync();
        var handler = Mocker.CreateInstance<GetLeagueWeekLineupsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetLeagueWeekLineupsQuery(LeagueId, Alice, 2026, 5));

        result.IsSuccess.Should().BeTrue();
        var alice = result.Value.Members.Single(m => m.UserId == Alice);
        alice.Slots.Select(s => s.SlotId).Should().BeEquivalentTo(["QB", "K"]);
        alice.HiddenSlotCount.Should().Be(0);

        // Bob's future-game kicker stays private until it locks.
        var bob = result.Value.Members.Single(m => m.UserId == Bob);
        bob.Slots.Select(s => s.SlotId).Should().BeEquivalentTo(["QB"]);
        bob.HiddenSlotCount.Should().Be(1);
    }

    [Fact]
    public async Task MembersWithoutALineupAppear_AndRowsOrderByPoints()
    {
        await SeedAsync();
        var handler = Mocker.CreateInstance<GetLeagueWeekLineupsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetLeagueWeekLineupsQuery(LeagueId, Alice, 2026, 5));

        result.Value.Members.Select(m => m.DisplayName).Should().Equal("Bob", "Alice", "Carol");
        var carol = result.Value.Members.Single(m => m.UserId == Carol);
        carol.Slots.Should().BeEmpty();
        carol.TotalPoints.Should().Be(0m);
    }

    [Fact]
    public async Task TeamPickemLeague_IsForbidden()
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = LeagueId,
            Name = "Team",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            CommissionerUserId = Alice,
            SeasonYear = 2026,
            GroupType = GroupType.TeamPickem,
        });
        await DataContext.SaveChangesAsync();
        var handler = Mocker.CreateInstance<GetLeagueWeekLineupsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetLeagueWeekLineupsQuery(LeagueId, Alice, 2026, 5));

        result.Status.Should().Be(ResultStatus.Forbid);
    }
}
