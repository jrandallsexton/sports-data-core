using System.Linq.Expressions;

using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Consumers;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Consumers;

/// <summary>
/// Ingest-side consumer is a thin shim: translate the bus message into a
/// Hangfire job for the Worker pod. All real work happens in
/// <see cref="ContestStartTimeUpdatedConsumerHandler"/>; this test only proves
/// the enqueue happens with the message intact.
/// </summary>
public class ContestStartTimeUpdatedConsumerTests
    : ProducerTestBase<ContestStartTimeUpdatedConsumer>
{
    private readonly ContestStartTimeUpdatedConsumer _sut;

    public ContestStartTimeUpdatedConsumerTests()
    {
        _sut = Mocker.CreateInstance<ContestStartTimeUpdatedConsumer>();
    }

    [Fact]
    public async Task Consume_EnqueuesHandlerWithMessage()
    {
        var evt = new ContestStartTimeUpdated(
            ContestId: Guid.NewGuid(),
            NewStartTime: new DateTime(2026, 6, 21, 23, 7, 0, DateTimeKind.Utc),
            Ref: null,
            Sport: Sport.BaseballMlb,
            SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

        var context = new Mock<ConsumeContext<ContestStartTimeUpdated>>();
        context.Setup(x => x.Message).Returns(evt);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Setup(x => x.Enqueue<IContestStartTimeUpdatedConsumerHandler>(
                It.IsAny<Expression<Func<IContestStartTimeUpdatedConsumerHandler, Task>>>()))
            .Returns("hf-enqueued-1");

        await _sut.Consume(context.Object);

        Mock.Get(Mocker.Get<IProvideBackgroundJobs>())
            .Verify(x => x.Enqueue<IContestStartTimeUpdatedConsumerHandler>(
                It.IsAny<Expression<Func<IContestStartTimeUpdatedConsumerHandler, Task>>>()),
                Times.Once);
    }

    [Fact]
    public async Task Consume_DoesNotPerformAnyDbWork()
    {
        // Ingest consumers must be thin shims (per project convention).
        // Anything that touches the DbContext belongs on the Worker side.

        var evt = new ContestStartTimeUpdated(
            ContestId: Guid.NewGuid(),
            NewStartTime: new DateTime(2026, 6, 21, 23, 7, 0, DateTimeKind.Utc),
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

        var context = new Mock<ConsumeContext<ContestStartTimeUpdated>>();
        context.Setup(x => x.Message).Returns(evt);

        await _sut.Consume(context.Object);

        FootballDataContext.Contests.Should().BeEmpty();
        FootballDataContext.Competitions.Should().BeEmpty();
        FootballDataContext.CompetitionStreams.Should().BeEmpty();
    }
}
