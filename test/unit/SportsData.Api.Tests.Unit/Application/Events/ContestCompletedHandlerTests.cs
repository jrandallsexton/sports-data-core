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
    public class ContestCompletedHandlerTests : ApiTestBase<ContestCompletedHandler>
    {
        [Fact]
        public async Task Consume_EnqueuesScoreContestCommandForContestId()
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

            // Assert — verify the enqueued expression invokes Process(cmd) with
            // a ScoreContestCommand carrying the expected ContestId and the
            // CorrelationId propagated from the event. Catches regressions in
            // the method call shape, command type, or field wiring.
            background.Verify(x => x.Enqueue<IScoreContests>(
                It.Is<Expression<Func<IScoreContests, Task>>>(expr =>
                    ScoreContestCommandFromExpression(expr) != null
                    && ScoreContestCommandFromExpression(expr)!.ContestId == contestId
                    && ScoreContestCommandFromExpression(expr)!.CorrelationId == correlationId
                )),
                Times.Once);
        }

        /// <summary>
        /// Compiles and evaluates the single argument of a
        /// <c>p =&gt; p.Process(cmd)</c> expression to extract the captured
        /// <see cref="ScoreContestCommand"/> instance. Returns null when the
        /// expression isn't shaped as expected (wrong method, wrong arity, or
        /// non-<see cref="ScoreContestCommand"/> argument).
        /// </summary>
        private static ScoreContestCommand? ScoreContestCommandFromExpression(
            Expression<Func<IScoreContests, Task>> expr)
        {
            if (expr.Body is not MethodCallExpression call) return null;
            if (call.Method.Name != nameof(IScoreContests.Process)) return null;
            if (call.Arguments.Count != 1) return null;

            return Expression.Lambda<Func<ScoreContestCommand>>(call.Arguments[0]).Compile()();
        }
    }
}
