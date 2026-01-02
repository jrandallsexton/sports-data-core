//using FluentAssertions;
using SportsData.Api.Application.Common.Enums;

//using Microsoft.EntityFrameworkCore;
//using Microsoft.Extensions.Logging.Abstractions;

//using SportsData.Api.Application;
//using SportsData.Api.Application.Scoring;
//using SportsData.Api.Infrastructure.Data;
//using SportsData.Api.Infrastructure.Data.Entities;
//using SportsData.Core.Common;

//using Xunit;

//namespace SportsData.Api.Tests.Unit.Application.Scoring
//{
//    public class PickScoringJobTests
//    {
//        private static DbContextOptions<AppDataContext> CreateOptions(string dbName) =>
//            new DbContextOptionsBuilder<AppDataContext>()
//                .UseInMemoryDatabase(databaseName: dbName)
//                .Options;

//        private static async Task SeedTestDataAsync(AppDataContext db, Guid contestId, Guid franchiseId)
//        {
//            var league = new Infrastructure.Data.Entities.PickemGroup
//            {
//                Id = Guid.NewGuid(),
//                Name = "Test League",
//                Sport = Sport.FootballNcaa,
//                League = Api.Application.League.NCAAF,
//                CommissionerUserId = Guid.NewGuid(),
//                PickType = PickType.StraightUp,
//                UseConfidencePoints = false
//            };

//            var contest = new Contest
//            {
//                Id = Guid.NewGuid(),
//                ContestId = contestId,
//                HomeFranchiseId = franchiseId,
//                AwayFranchiseId = Guid.NewGuid(),
//                WinnerFranchiseId = franchiseId,
//                SpreadWinnerFranchiseId = franchiseId,
//                HomeScore = 28,
//                AwayScore = 17,
//                OverUnder = 44.5,
//                FinalizedUtc = DateTime.UtcNow
//            };

//            var pick = new PickemGroupUserPick
//            {
//                Id = Guid.NewGuid(),
//                UserId = Guid.NewGuid(),
//                PickemGroupId = league.Id,
//                ContestId = contestId,
//                FranchiseId = franchiseId
//            };

//            await db.PickemGroups.AddAsync(league);
//            await db.Contests.AddAsync(contest);
//            await db.UserPicks.AddAsync(pick);

//            await db.SaveChangesAsync();
//        }

//        [Fact]
//        public async Task ScoreAllAsync_ShouldScoreEligiblePicks()
//        {
//            // Arrange
//            var contestId = Guid.NewGuid();
//            var teamId = Guid.NewGuid();

//            var options = CreateOptions(nameof(ScoreAllAsync_ShouldScoreEligiblePicks));
//            await using var db = new AppDataContext(options);

//            await SeedTestDataAsync(db, contestId, teamId);

//            var scoringService = new PickScoringService();
//            var job = new PickScoringJob(db, scoringService, NullLogger<PickScoringJob>.Instance);

//            // Act
//            await job.ScoreAllAsync();

//            // Assert
//            var pick = await db.UserPicks.SingleAsync();
//            pick.IsCorrect.Should().BeTrue();
//            pick.PointsAwarded.Should().Be(1);
//            pick.ScoredAt.Should().NotBeNull();
//        }

//        [Fact]
//        public async Task ScoreAllAsync_ShouldSkip_UnfinalizedContests()
//        {
//            var contestId = Guid.NewGuid();
//            var teamId = Guid.NewGuid();

//            var options = CreateOptions(nameof(ScoreAllAsync_ShouldSkip_UnfinalizedContests));
//            await using var db = new AppDataContext(options);

//            var league = new Infrastructure.Data.Entities.PickemGroup
//            {
//                Id = Guid.NewGuid(),
//                Name = "Unfinalized League",
//                Sport = Sport.FootballNcaa,
//                League = Api.Application.League.NCAAF,
//                CommissionerUserId = Guid.NewGuid(),
//                PickType = PickType.StraightUp,
//                UseConfidencePoints = false
//            };

//            var contest = new Contest
//            {
//                Id = Guid.NewGuid(),
//                ContestId = contestId,
//                HomeFranchiseId = teamId,
//                AwayFranchiseId = Guid.NewGuid(),
//                FinalizedUtc = null
//            };

//            var pick = new PickemGroupUserPick
//            {
//                Id = Guid.NewGuid(),
//                UserId = Guid.NewGuid(),
//                PickemGroupId = league.Id,
//                ContestId = contestId,
//                FranchiseId = teamId
//            };

//            db.PickemGroups.Add(league);
//            db.Contests.Add(contest);
//            db.UserPicks.Add(pick);

//            await db.SaveChangesAsync();

//            var job = new PickScoringJob(db, new PickScoringService(), NullLogger<PickScoringJob>.Instance);

//            // Act
//            await job.ScoreAllAsync();

//            // Assert
//            var result = await db.UserPicks.SingleAsync();
//            result.IsCorrect.Should().BeNull();
//            result.PointsAwarded.Should().BeNull();
//            result.ScoredAt.Should().BeNull();
//        }
//    }
//}
