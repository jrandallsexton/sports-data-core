using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring
{
    public class LeagueWeekResultGeneratorTests
    {
        private AppDataContext CreateContext(string dbName)
        {
            var options = new DbContextOptionsBuilder<AppDataContext>()
                .UseInMemoryDatabase(databaseName: dbName)
                .Options;

            return new AppDataContext(options);
        }

        [Fact]
        public async Task Picks_With_Same_TotalPoints_Are_Broken_By_Tiebreaker()
        {
            var leagueId = Guid.NewGuid();
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();

            var contestId = Guid.NewGuid();

            await using var context = CreateContext(nameof(Picks_With_Same_TotalPoints_Are_Broken_By_Tiebreaker));

            context.PickemGroups.Add(new Infrastructure.Data.Entities.PickemGroup
            {
                Id = leagueId,
                Name = "Test League",
                Sport = Sport.FootballNcaa,
                League = Api.Application.League.NCAAF,
                CommissionerUserId = Guid.NewGuid(),
                UseConfidencePoints = false,
                PickType = PickType.StraightUp,
                TiebreakerType = TiebreakerType.TotalPoints
            });

            context.Contests.Add(new Contest
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                Sport = Sport.FootballNcaa,
                SeasonYear = 2024,
                SeasonWeek = 1,
                StartUtc = DateTime.UtcNow.AddDays(-1),
                FinalizedUtc = DateTime.UtcNow
            });

            context.UserPicks.AddRange(
                new PickemGroupUserPick
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userA,
                    ContestId = contestId,
                    PointsAwarded = 10,
                    IsCorrect = true,
                    ScoredAt = DateTime.UtcNow,
                    TiebreakerType = TiebreakerType.TotalPoints,
                    TiebreakerGuessTotal = 48,
                    TiebreakerActualTotal = 50
                },
                new PickemGroupUserPick
                {
                    Id = Guid.NewGuid(),
                    PickemGroupId = leagueId,
                    UserId = userB,
                    ContestId = contestId,
                    PointsAwarded = 10,
                    IsCorrect = true,
                    ScoredAt = DateTime.UtcNow,
                    TiebreakerType = TiebreakerType.TotalPoints,
                    TiebreakerGuessTotal = 52,
                    TiebreakerActualTotal = 50
                }
            );

            await context.SaveChangesAsync();

            var generator = new LeagueWeekResultGenerator(context, NullLogger<LeagueWeekResultGenerator>.Instance);
            await generator.GenerateAsync();

            var results = await context.PickemGroupWeekResults
                .Where(r => r.PickemGroupId == leagueId && r.SeasonYear == 2024 && r.SeasonWeek == 1)
                .ToListAsync();

            results.Should().HaveCount(2);

            var winners = results.Where(r => r.IsWeeklyWinner).ToList();
            winners.Should().ContainSingle(); // Asserts exactly one winner
            var winner = winners.Single();

        }
    }
}
