using FluentAssertions;

using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring
{
    public class PickScoringServiceTests
    {
        private readonly PickScoringService _service = new();

        private static Infrastructure.Data.Entities.PickemGroup GetDefaultLeague(
            PickType pickType,
            bool useConfidence = false) =>
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Test League",
                Sport = Sport.FootballNcaa,
                League = Api.Application.League.NCAAF,
                PickType = pickType,
                UseConfidencePoints = useConfidence,
                CommissionerUserId = Guid.NewGuid()
            };

        private static Contest GetFinalizedContest(
            Guid contestId,
            Guid winnerId,
            Guid spreadWinnerId,
            int homeScore = 28,
            int awayScore = 17,
            double? overUnder = 44.5) =>
            new()
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                HomeFranchiseId = Guid.NewGuid(),
                AwayFranchiseId = Guid.NewGuid(),
                WinnerFranchiseId = winnerId,
                SpreadWinnerFranchiseId = spreadWinnerId,
                HomeScore = homeScore,
                AwayScore = awayScore,
                OverUnder = overUnder,
                FinalizedUtc = DateTime.UtcNow
            };

        [Fact]
        public void Scores_StraightUp_Correctly()
        {
            var contestId = Guid.NewGuid();
            var winnerId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = winnerId,
                PickType = PickType.StraightUp
            };

            var league = GetDefaultLeague(PickType.StraightUp);
            var contest = GetFinalizedContest(contestId, winnerId, Guid.NewGuid());

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeTrue();
            result.PointsAwarded.Should().Be(1);
        }

        [Fact]
        public void Scores_ATS_Correctly()
        {
            var contestId = Guid.NewGuid();
            var atsWinnerId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = atsWinnerId,
                PickType = PickType.AgainstTheSpread
            };

            var league = GetDefaultLeague(PickType.AgainstTheSpread);
            var contest = GetFinalizedContest(contestId, Guid.NewGuid(), atsWinnerId);

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeTrue();
            result.PointsAwarded.Should().Be(1);
            result.WasAgainstSpread.Should().BeTrue();
        }

        [Fact]
        public void Scores_OverUnder_Correctly()
        {
            var contestId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                OverUnder = OverUnderPick.Over,
                PickType = PickType.OverUnder
            };

            var league = GetDefaultLeague(PickType.OverUnder);
            var contest = GetFinalizedContest(contestId, Guid.NewGuid(), Guid.NewGuid(), 35, 20, 52.5); // Total = 55

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeTrue();
            result.PointsAwarded.Should().Be(1);
        }

        [Fact]
        public void Applies_ConfidencePoints_WhenEnabled_AndCorrect()
        {
            var contestId = Guid.NewGuid();
            var winnerId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = winnerId,
                ConfidencePoints = 5,
                PickType = PickType.StraightUp | PickType.Confidence
            };

            var league = GetDefaultLeague(PickType.StraightUp | PickType.Confidence, useConfidence: true);
            var contest = GetFinalizedContest(contestId, winnerId, Guid.NewGuid());

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeTrue();
            result.PointsAwarded.Should().Be(5);
        }

        [Fact]
        public void Scores_IncorrectPick_AsZeroPoints()
        {
            var contestId = Guid.NewGuid();
            var correctId = Guid.NewGuid();
            var incorrectId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = incorrectId,
                PickType = PickType.StraightUp
            };

            var league = GetDefaultLeague(PickType.StraightUp);
            var contest = GetFinalizedContest(contestId, correctId, Guid.NewGuid());

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeFalse();
            result.PointsAwarded.Should().Be(0);
        }

        [Fact]
        public void Throws_If_ContestNotFinalized()
        {
            var contestId = Guid.NewGuid();
            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FinalizedUtc = null
            };

            var league = GetDefaultLeague(PickType.StraightUp);
            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = Guid.NewGuid(),
                PickType = PickType.StraightUp
            };

            var act = () => _service.ScorePick(pick, contest, league);

            act.Should().Throw<InvalidOperationException>().WithMessage("*not been finalized*");
        }

        [Fact]
        public void Applies_ConfidencePoints_ButZero_IfIncorrect()
        {
            var contestId = Guid.NewGuid();
            var winnerId = Guid.NewGuid();
            var wrongId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = wrongId,
                ConfidencePoints = 7,
                PickType = PickType.StraightUp | PickType.Confidence
            };

            var league = GetDefaultLeague(PickType.StraightUp | PickType.Confidence, useConfidence: true);
            var contest = GetFinalizedContest(contestId, winnerId, Guid.NewGuid());

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeFalse();
            result.PointsAwarded.Should().Be(0);
        }

        [Fact]
        public void OverUnder_Push_ShouldNotScore()
        {
            var contestId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                OverUnder = OverUnderPick.Over,
                PickType = PickType.OverUnder
            };

            var league = GetDefaultLeague(PickType.OverUnder);
            var contest = GetFinalizedContest(contestId, Guid.NewGuid(), Guid.NewGuid(), 24, 24, 48.0); // Total = 48, line = 48

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeFalse();
            result.PointsAwarded.Should().Be(0);
        }

        [Fact]
        public void MultiPickType_AllCorrect_ShouldAccumulatePoints()
        {
            var contestId = Guid.NewGuid();
            var teamId = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = teamId,
                OverUnder = OverUnderPick.Over,
                PickType = PickType.StraightUp | PickType.AgainstTheSpread | PickType.OverUnder
            };

            var league = GetDefaultLeague(pick.PickType);
            var contest = GetFinalizedContest(contestId, teamId, teamId, 35, 20, 52.5); // Total = 55, line = 52.5

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeTrue(); // at least one correct
            result.PointsAwarded.Should().Be(3);
        }

        [Fact]
        public void StraightUp_TieGame_ShouldNotScoreAsCorrect()
        {
            var contestId = Guid.NewGuid();
            var pickedTeam = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = pickedTeam,
                PickType = PickType.StraightUp
            };

            var league = GetDefaultLeague(PickType.StraightUp);

            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                HomeFranchiseId = pickedTeam, // Could be home or away
                AwayFranchiseId = Guid.NewGuid(),
                WinnerFranchiseId = null, // Tied
                HomeScore = 24,
                AwayScore = 24,
                FinalizedUtc = DateTime.UtcNow
            };

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeFalse();
            result.PointsAwarded.Should().Be(0);
        }

        [Fact]
        public void NFL_TieGame_ShouldScoreIncorrect()
        {
            var contestId = Guid.NewGuid();
            var pickedTeam = Guid.NewGuid();

            var pick = new PickemGroupUserPick
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                FranchiseId = pickedTeam,
                PickType = PickType.StraightUp
            };

            var league = new Infrastructure.Data.Entities.PickemGroup
            {
                Id = Guid.NewGuid(),
                Name = "NFL League",
                Sport = Sport.FootballNfl,
                League = Api.Application.League.NFL,
                PickType = PickType.StraightUp,
                UseConfidencePoints = false,
                CommissionerUserId = Guid.NewGuid()
            };

            var contest = new Contest
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,
                HomeFranchiseId = pickedTeam,
                AwayFranchiseId = Guid.NewGuid(),
                WinnerFranchiseId = null,
                HomeScore = 17,
                AwayScore = 17,
                FinalizedUtc = DateTime.UtcNow
            };

            var result = _service.ScorePick(pick, contest, league);

            result.IsCorrect.Should().BeFalse();
            result.PointsAwarded.Should().Be(0);
        }

    }
}
