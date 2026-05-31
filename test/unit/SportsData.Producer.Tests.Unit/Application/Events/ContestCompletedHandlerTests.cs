using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Application.Events;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Events;

public class ContestCompletedHandlerTests : ProducerTestBase<ContestCompletedHandler>
{
    [Fact]
    public async Task Consume_SchedulesEnrichContestCommandWithDelay()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();

        var message = new ContestCompleted(
            ContestId: contestId,
            CompetitionId: competitionId,
            SeasonWeekId: Guid.NewGuid(),
            Ref: null,
            Sport: Sport.BaseballMlb,
            SeasonYear: 2026,
            CorrelationId: correlationId,
            CausationId: correlationId);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var context = Mock.Of<ConsumeContext<ContestCompleted>>(ctx =>
            ctx.Message == message);

        var sut = Mocker.CreateInstance<ContestCompletedHandler>();

        // Act
        await sut.Consume(context);

        // Assert — verify Schedule was invoked with an expression shaped as
        // p => p.Process(cmd) where cmd is an EnrichContestCommand carrying
        // the event's ContestId + CorrelationId, AND the delay is the
        // status-propagation buffer (30s — see
        // docs/contest-finalization-event-restructure.md D2). Pins the
        // call shape against regressions in event field plumbing or a
        // mistaken use of Enqueue (immediate) instead of Schedule (delayed).
        background.Verify(x => x.Schedule<IEnrichContests>(
            It.Is<Expression<Func<IEnrichContests, Task>>>(expr =>
                ScheduleInvokesProcessWith(expr, contestId, correlationId)),
            It.Is<TimeSpan>(t => t == TimeSpan.FromSeconds(30))),
            Times.Once);

        // And ensure Enqueue was NOT used — the whole point of the restructure
        // is that we defer enrichment to give status propagation a window.
        background.Verify(x => x.Enqueue<IEnrichContests>(
            It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    /// <summary>
    /// True iff <paramref name="expr"/> is shaped as
    /// <c>p =&gt; p.Process(cmd)</c> where <c>cmd</c> is an
    /// <see cref="EnrichContestCommand"/> whose <c>ContestId</c> and
    /// <c>CorrelationId</c> match the expected values. Same compile-and-eval
    /// trick as ContestFinalizedHandlerTests in the API project.
    /// </summary>
    private static bool ScheduleInvokesProcessWith(
        Expression<Func<IEnrichContests, Task>> expr,
        Guid expectedContestId,
        Guid expectedCorrelationId)
    {
        if (expr.Body is not MethodCallExpression call) return false;
        if (call.Method.Name != nameof(IEnrichContests.Process)) return false;
        if (call.Arguments.Count != 1) return false;

        var cmd = Expression.Lambda<Func<EnrichContestCommand>>(call.Arguments[0]).Compile()();
        return cmd != null
            && cmd.ContestId == expectedContestId
            && cmd.CorrelationId == expectedCorrelationId;
    }
}
