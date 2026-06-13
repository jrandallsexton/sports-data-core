using MassTransit;

using Moq;

using SportsData.Api.Application.Events;
using SportsData.Api.Application.Scoring;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Events
{
    public class ContestFinalizedHandlerTests : ApiTestBase<ContestFinalizedHandler>
    {
        [Fact]
        public async Task Consume_EnqueuesScoreContestCommandForContestId()
        {
            // Arrange
            var contestId = Guid.NewGuid();
            var correlationId = Guid.NewGuid();

            var message = new ContestFinalized(
                ContestId: contestId,
                Ref: null,
                Sport: Sport.BaseballMlb,
                SeasonYear: 2026,
                CorrelationId: correlationId,
                CausationId: correlationId);

            var background = Mocker.GetMock<IProvideBackgroundJobs>();

            var context = Mock.Of<ConsumeContext<ContestFinalized>>(ctx =>
                ctx.Message == message);

            var sut = Mocker.CreateInstance<ContestFinalizedHandler>();

            // Act
            await sut.Consume(context);

            // Assert — verify the enqueued expression invokes Process(cmd) with
            // a ScoreContestCommand carrying the expected ContestId and the
            // CorrelationId propagated from the event. Catches regressions in
            // the method call shape, command type, or field wiring.
            background.Verify(x => x.Enqueue<IScorePicks>(
                It.Is<Expression<Func<IScorePicks, Task>>>(expr =>
                    EnqueueInvokesProcessWith(expr, contestId, correlationId))),
                Times.Once);
        }

        /// <summary>
        /// True iff <paramref name="expr"/> is shaped as
        /// <c>p =&gt; p.Process(cmd)</c> where <c>cmd</c> is a
        /// <see cref="ScorePicksCommand"/> whose <c>ContestId</c> and
        /// <c>CorrelationId</c> match the expected values. The argument
        /// expression is compiled and evaluated exactly once so the predicate
        /// inside <c>It.Is&lt;...&gt;</c> stays a single helper call (the
        /// expression-tree form of <c>It.Is</c> can't host a block-bodied
        /// lambda with a local).
        /// </summary>
        private static bool EnqueueInvokesProcessWith(
            Expression<Func<IScorePicks, Task>> expr,
            Guid expectedContestId,
            Guid expectedCorrelationId)
        {
            if (expr.Body is not MethodCallExpression call) return false;
            if (call.Method.Name != nameof(IScorePicks.Process)) return false;
            if (call.Arguments.Count != 1) return false;

            var cmd = Expression.Lambda<Func<ScorePicksCommand>>(call.Arguments[0]).Compile()();
            return cmd != null
                && cmd.ContestId == expectedContestId
                && cmd.CorrelationId == expectedCorrelationId;
        }
    }
}
