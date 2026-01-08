using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.SeasonWeek.Commands.EnqueueSeasonWeekContestsUpdate;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.SeasonWeeks.Commands;

public class EnqueueSeasonWeekContestsUpdateCommandHandlerTests :
    ProducerTestBase<EnqueueSeasonWeekContestsUpdateCommandHandler>
{
    [Fact]
    public async Task WhenContestsExist_ShouldEnqueueUpdateJobsForEach()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueSeasonWeekContestsUpdateCommandHandler>();

        var seasonWeekId = Guid.NewGuid();

        // Create contests for the season week
        for (int i = 0; i < 5; i++)
        {
            var contest = CreateContest(seasonWeekId, 2024);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new EnqueueSeasonWeekContestsUpdateCommand(seasonWeekId);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        result.Value.Should().Be(seasonWeekId);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IUpdateContests, Task>>>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task WhenNoContestsExist_ShouldReturnSuccessWithNoEnqueues()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueSeasonWeekContestsUpdateCommandHandler>();

        var seasonWeekId = Guid.NewGuid();
        var command = new EnqueueSeasonWeekContestsUpdateCommand(seasonWeekId);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IUpdateContests, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenContestsExistForDifferentSeasonWeek_ShouldNotEnqueueThoseContests()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueSeasonWeekContestsUpdateCommandHandler>();

        var targetSeasonWeekId = Guid.NewGuid();
        var otherSeasonWeekId = Guid.NewGuid();

        // Create contests for target season week
        for (int i = 0; i < 2; i++)
        {
            var contest = CreateContest(targetSeasonWeekId, 2024);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        // Create contests for other season week
        for (int i = 0; i < 3; i++)
        {
            var contest = CreateContest(otherSeasonWeekId, 2024);
            await FootballDataContext.Contests.AddAsync(contest);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new EnqueueSeasonWeekContestsUpdateCommand(targetSeasonWeekId);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IUpdateContests, Task>>>()),
            Times.Exactly(2));
    }

    private Contest CreateContest(Guid seasonWeekId, int seasonYear)
    {
        return Fixture.Build<Contest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Game")
            .With(x => x.ShortName, "Test")
            .With(x => x.SeasonWeekId, seasonWeekId)
            .With(x => x.SeasonYear, seasonYear)
            .Create();
    }
}
