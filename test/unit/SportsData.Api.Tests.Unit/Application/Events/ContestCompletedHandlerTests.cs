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

            // Assert
            background.Verify(x => x.Enqueue<IScoreContests>(
                It.IsAny<Expression<Func<IScoreContests, Task>>>()), Times.Once);
        }
    }
}
