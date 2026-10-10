#nullable enable
using System.Linq.Expressions;

using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class FootballPlayCompletedConsumerTests : ProducerTestBase<FootballPlayCompletedConsumer>
{
    [Fact]
    public async Task Consume_WhenKillSwitchOff_DoesNotEnqueue()
    {
        Mocker.Use(new InstantAnalysisConfig { Enabled = false });
        var sut = Mocker.CreateInstance<FootballPlayCompletedConsumer>();

        await sut.Consume(ContextFor(BuildEvent()));

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>()).Verify(x => x.Enqueue(
                It.IsAny<Expression<Func<IFootballPlayCompletedConsumerHandler, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_WhenKillSwitchOn_EnqueuesAnalysisJob()
    {
        Mocker.Use(new InstantAnalysisConfig { Enabled = true });
        var sut = Mocker.CreateInstance<FootballPlayCompletedConsumer>();

        await sut.Consume(ContextFor(BuildEvent()));

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>()).Verify(x => x.Enqueue(
                It.IsAny<Expression<Func<IFootballPlayCompletedConsumerHandler, Task>>>()),
            Times.Once);
    }

    [Fact]
    public void AnalysisJob_IsRoutedToTheLiveQueue()
    {
        // The queue is set by attribute (the Enqueue(queue, ...) overload is
        // ignored by our Hangfire storage), so assert the attribute itself.
        typeof(IFootballPlayCompletedConsumerHandler)
            .GetMethod(nameof(IFootballPlayCompletedConsumerHandler.Process))!
            .GetCustomAttributes(typeof(Hangfire.QueueAttribute), inherit: false)
            .Cast<Hangfire.QueueAttribute>()
            .Should().ContainSingle(a => a.Queue == HangfireQueues.Live);
    }

    [Fact]
    public void Definition_UsesItsOwnQueue_NotTheApiConsumersQueue()
    {
        FootballPlayCompletedConsumerDefinition.QueueName
            .Should().Be("producer-football-play-completed")
            .And.NotBe("football-play-completed");
    }

    private static ConsumeContext<FootballPlayCompleted> ContextFor(FootballPlayCompleted evt)
    {
        var context = new Mock<ConsumeContext<FootballPlayCompleted>>();
        context.Setup(x => x.Message).Returns(evt);
        return context.Object;
    }

    internal static FootballPlayCompleted BuildEvent(Guid? competitionId = null, Guid? playId = null) =>
        new(
            ContestId: Guid.NewGuid(),
            CompetitionId: competitionId ?? Guid.NewGuid(),
            PlayId: playId ?? Guid.NewGuid(),
            PlayDescription: "pass intercepted",
            Period: "Q2",
            Clock: "5:00",
            AwayScore: 7,
            HomeScore: 3,
            PossessionFranchiseSeasonId: null,
            IsScoringPlay: false,
            ScoringPlayType: null,
            BallOnYardLine: null,
            Down: null,
            Distance: null,
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());
}
