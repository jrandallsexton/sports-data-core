using FluentAssertions;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetPlayerStandings;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.PlayerLineups;

/// <summary>
/// Cumulative-points standings with weekly winners: ordering by season
/// total, per-week winner badges (ties share), zero-point weeks never
/// win, and the PlayerPickem gate applies.
/// </summary>
public class GetPlayerStandingsQueryHandlerTests : ApiTestBase<GetPlayerStandingsQueryHandler>
{
    private static readonly Guid LeagueId = Guid.NewGuid();
    private static readonly Guid Alice = Guid.NewGuid();
    private static readonly Guid Bob = Guid.NewGuid();

    private async Task SeedAsync()
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = LeagueId,
            Name = "PP",
            Sport = Sport.FootballNfl,
            League = League.NFL,
            CommissionerUserId = Alice,
            SeasonYear = 2026,
            GroupType = GroupType.PlayerPickem,
        });
        foreach (var (id, name) in new[] { (Alice, "Alice"), (Bob, "Bob") })
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

        void AddLineup(Guid userId, int week, decimal total, bool final)
        {
            var lineup = new PlayerLineup
            {
                Id = Guid.NewGuid(),
                PickemGroupId = LeagueId,
                UserId = userId,
                SeasonYear = 2026,
                SeasonWeek = week,
                TotalPoints = total,
                ScoreUpdatedUtc = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = userId,
            };
            lineup.Slots.Add(new PlayerLineupSlot
            {
                Id = Guid.NewGuid(),
                PlayerLineupId = lineup.Id,
                SlotId = "QB",
                AthleteId = Guid.NewGuid(),
                AthleteSeasonId = Guid.NewGuid(),
                Position = "QB",
                FirstName = "F",
                LastName = "L",
                TeamName = "T",
                TeamSlug = "t",
                Points = total,
                IsScoreFinal = final,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = userId,
            });
            DataContext.PlayerLineups.Add(lineup);
        }

        // Week 1: Alice 20 (final) beats Bob 10 (final).
        // Week 2: Bob 30 (live) beats Alice 5 (live).
        AddLineup(Alice, 1, 20m, final: true);
        AddLineup(Bob, 1, 10m, final: true);
        AddLineup(Alice, 2, 5m, final: false);
        AddLineup(Bob, 2, 30m, final: false);
        await DataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Standings_OrderBySeasonTotal_WithWeeklyWinners()
    {
        await SeedAsync();
        var handler = Mocker.CreateInstance<GetPlayerStandingsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetPlayerStandingsQuery(LeagueId, Alice, 2026));

        result.IsSuccess.Should().BeTrue();
        var rows = result.Value.Rows;
        rows.Should().HaveCount(2);
        // Bob 40 total leads Alice 25.
        rows[0].DisplayName.Should().Be("Bob");
        rows[0].TotalPoints.Should().Be(40m);
        rows[1].TotalPoints.Should().Be(25m);
        // One weekly win each: Alice week 1 (final), Bob week 2 (live).
        rows.Single(r => r.DisplayName == "Alice").WeeklyWins.Should().Be(1);
        rows.Single(r => r.DisplayName == "Bob").WeeklyWins.Should().Be(1);
        // Finality flags flow through.
        rows[0].Weeks.Single(w => w.Week == 1).IsFinal.Should().BeTrue();
        rows[0].Weeks.Single(w => w.Week == 2).IsFinal.Should().BeFalse();
    }

    [Fact]
    public async Task TeamPickemLeague_IsForbidden()
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = LeagueId,
            Name = "Team",
            Sport = Sport.FootballNfl,
            League = League.NFL,
            CommissionerUserId = Alice,
            SeasonYear = 2026,
            GroupType = GroupType.TeamPickem,
        });
        await DataContext.SaveChangesAsync();
        var handler = Mocker.CreateInstance<GetPlayerStandingsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetPlayerStandingsQuery(LeagueId, Alice, 2026));

        result.Status.Should().Be(ResultStatus.Forbid);
    }
}
