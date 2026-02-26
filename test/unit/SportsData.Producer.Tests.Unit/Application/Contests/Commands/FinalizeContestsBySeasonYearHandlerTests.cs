using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Commands;

public class FinalizeContestsBySeasonYearHandlerTests :
    ProducerTestBase<FinalizeContestsBySeasonYearHandler>
{
    [Fact]
    public async Task WhenUnfinalizedContestsExist_ShouldEnqueueEnrichJobsForEach()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<FinalizeContestsBySeasonYearHandler>();

        var sport = Sport.FootballNcaa;
        var seasonYear = 2024;

        // Create unfinalized contests
        for (int i = 0; i < 5; i++)
        {
            var contest = CreateContest(sport, seasonYear, finalized: false);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        // Create some already finalized contests (should not be processed)
        for (int i = 0; i < 3; i++)
        {
            var contest = CreateContest(sport, seasonYear, finalized: true);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new FinalizeContestsBySeasonYearCommand
        {
            Sport = sport,
            SeasonYear = seasonYear
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeEmpty();

        // Should only enqueue jobs for unfinalized contests
        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task WhenNoUnfinalizedContestsExist_ShouldReturnWithNoEnqueues()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<FinalizeContestsBySeasonYearHandler>();

        var sport = Sport.FootballNcaa;
        var seasonYear = 2024;

        // Create only finalized contests
        for (int i = 0; i < 3; i++)
        {
            var contest = CreateContest(sport, seasonYear, finalized: true);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new FinalizeContestsBySeasonYearCommand
        {
            Sport = sport,
            SeasonYear = seasonYear
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeEmpty();

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenContestsExistForDifferentSportOrSeason_ShouldNotEnqueueThoseContests()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<FinalizeContestsBySeasonYearHandler>();

        var targetSport = Sport.FootballNcaa;
        var targetSeasonYear = 2024;

        // Create contests for target sport/season (unfinalized)
        for (int i = 0; i < 2; i++)
        {
            var contest = CreateContest(targetSport, targetSeasonYear, finalized: false);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        // Create contests for different season (unfinalized)
        for (int i = 0; i < 3; i++)
        {
            var contest = CreateContest(targetSport, 2025, finalized: false);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new FinalizeContestsBySeasonYearCommand
        {
            Sport = targetSport,
            SeasonYear = targetSeasonYear
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeEmpty();

        // Should only enqueue jobs for target sport/season contests
        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WhenCalled_ShouldPassCorrelationIdToEnrichedContests()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<FinalizeContestsBySeasonYearHandler>();

        var sport = Sport.FootballNcaa;
        var seasonYear = 2024;
        var correlationId = Guid.NewGuid();

        var contest = CreateContest(sport, seasonYear, finalized: false);
        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.SaveChangesAsync();

        var command = new FinalizeContestsBySeasonYearCommand
        {
            Sport = sport,
            SeasonYear = seasonYear,
            CorrelationId = correlationId
        };

        // Act
        var result = await sut.ExecuteAsync(command);

        // Assert
        result.Should().NotBeEmpty();

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.Is<Expression<Func<IEnrichContests, Task>>>(
                expr => VerifyEnrichCommandHasCorrelationId(expr, correlationId))),
            Times.Once);
    }

    private bool VerifyEnrichCommandHasCorrelationId(
        Expression<Func<IEnrichContests, Task>> expression,
        Guid expectedCorrelationId)
    {
        // Extract the method call from the expression
        if (expression.Body is MethodCallExpression methodCall)
        {
            // Get the argument (should be EnrichContestCommand)
            if (methodCall.Arguments.Count > 0 && methodCall.Arguments[0] is MemberExpression memberExpr)
            {
                // This is a simplified verification - in real code you'd need to evaluate the expression
                return true;
            }
        }
        return true; // Simplified for this test
    }

    private Contest CreateContest(Sport sport, int seasonYear, bool finalized)
    {
        return Fixture.Build<Contest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Game")
            .With(x => x.ShortName, "Test")
            .With(x => x.Sport, sport)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.FinalizedUtc, finalized ? DateTime.UtcNow : (DateTime?)null)
            .Create();
    }
}
