using AutoFixture;

using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Contests.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Commands;

public class FinalizeContestsBySeasonYearHandlerTests :
    ProducerTestBase<FinalizeContestsBySeasonYearHandler>
{
    public FinalizeContestsBySeasonYearHandlerTests()
    {
        // Register the validator
        Mocker.Use<IValidator<FinalizeContestsBySeasonYearCommand>>(
            new FinalizeContestsBySeasonYearCommandValidator());
    }

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
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        // Should only enqueue jobs for unfinalized contests
        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task WhenNoUnfinalizedContestsExist_ShouldReturnSuccessWithNoEnqueues()
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
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();

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
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();

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
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(correlationId);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.Is<Expression<Func<IEnrichContests, Task>>>(
                expr => VerifyEnrichCommandHasCorrelationId(expr, correlationId))),
            Times.Once);
    }

    [Fact]
    public async Task WhenValidationFails_ShouldReturnFailure()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<FinalizeContestsBySeasonYearHandler>();

        var command = new FinalizeContestsBySeasonYearCommand
        {
            Sport = Sport.FootballNcaa,
            SeasonYear = 1999, // Invalid: before 2000
            CorrelationId = Guid.NewGuid()
        };

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Failure<Guid>>();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);

        var failure = result as Failure<Guid>;
        failure!.Errors.Should().NotBeEmpty();

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    private bool VerifyEnrichCommandHasCorrelationId(
        Expression<Func<IEnrichContests, Task>> expression,
        Guid expectedCorrelationId)
    {
        // Extract the method call from the expression (e.g., p.Process(cmd))
        if (expression.Body is not MethodCallExpression methodCall)
        {
            return false;
        }

        // The first argument should be the EnrichContestCommand
        if (methodCall.Arguments.Count == 0)
        {
            return false;
        }

        var argument = methodCall.Arguments[0];

        // Extract the actual EnrichContestCommand instance from the expression tree
        var command = ExtractCommandFromExpression(argument);

        if (command is null)
        {
            return false;
        }

        // Verify the CorrelationId matches
        return command.CorrelationId == expectedCorrelationId;
    }

    private EnrichContestCommand? ExtractCommandFromExpression(Expression expression)
    {
        // Handle different expression wrapper types
        switch (expression)
        {
            // Direct member access (e.g., a captured variable)
            case MemberExpression memberExpr:
            {
                // Compile and evaluate to get the actual value
                var lambda = Expression.Lambda<Func<object>>(
                    Expression.Convert(memberExpr, typeof(object)));
                var compiled = lambda.Compile();
                return compiled() as EnrichContestCommand;
            }

            // Unary expression (e.g., Convert)
            case UnaryExpression unaryExpr:
                return ExtractCommandFromExpression(unaryExpr.Operand);

            // Constant value
            case ConstantExpression constantExpr:
                return constantExpr.Value as EnrichContestCommand;

            // New expression (e.g., new EnrichContestCommand(...))
            case NewExpression newExpr:
            {
                // Compile and evaluate the constructor call
                var lambda = Expression.Lambda<Func<EnrichContestCommand>>(newExpr);
                var compiled = lambda.Compile();
                return compiled();
            }

            default:
                // Try to compile the expression and evaluate it
                try
                {
                    var lambda = Expression.Lambda<Func<object>>(
                        Expression.Convert(expression, typeof(object)));
                    var compiled = lambda.Compile();
                    return compiled() as EnrichContestCommand;
                }
                catch (Exception ex)
                {
                    // Log the exception for debugging test failures
                    // Using Console.WriteLine so it appears in test output
                    Console.WriteLine(
                        $"Failed to extract EnrichContestCommand from expression: {expression.GetType().Name}. " +
                        $"Error: {ex.Message}\nStack Trace: {ex.StackTrace}");
                    return null;
                }
        }
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
