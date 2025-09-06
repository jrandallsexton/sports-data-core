using MassTransit;

using Moq;

using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Application.Previews;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.PickemGroups
{
    public class PickemGroupWeekMatchupsGeneratedHandlerTests : ApiTestBase<PickemGroupWeekMatchupsGeneratedHandler>
    {
        [Fact]
        public async Task Should_Enqueue_PreviewJobs_For_Contests_Without_Existing_Previews()
        {
            // Arrange
            var groupId = Guid.NewGuid();
            var contestId1 = Guid.NewGuid();
            var contestId2 = Guid.NewGuid();
            var contestId3 = Guid.NewGuid(); // This one already has a preview

            await DataContext.PickemGroupMatchups.AddRangeAsync(new[]
            {
                new PickemGroupMatchup { GroupId = groupId, SeasonYear = 2025, SeasonWeek = 1, ContestId = contestId1 },
                new PickemGroupMatchup { GroupId = groupId, SeasonYear = 2025, SeasonWeek = 1, ContestId = contestId2 },
                new PickemGroupMatchup { GroupId = groupId, SeasonYear = 2025, SeasonWeek = 1, ContestId = contestId3 }
            });

            await DataContext.MatchupPreviews.AddAsync(new MatchupPreview { ContestId = contestId3 });

            await DataContext.SaveChangesAsync();

            var message = new PickemGroupWeekMatchupsGenerated
            (
                groupId,
                2025,
                1,
                Guid.NewGuid(),
                Guid.NewGuid()
            );

            var background = Mocker.GetMock<IProvideBackgroundJobs>();

            var context = Mock.Of<ConsumeContext<PickemGroupWeekMatchupsGenerated>>(ctx =>
                ctx.Message == message);

            var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedHandler>();

            // Act
            await sut.Consume(context);

            // Assert
            background.Verify(x => x.Enqueue<MatchupPreviewProcessor>(
                It.IsAny<Expression<Func<MatchupPreviewProcessor, Task>>>()), Times.Exactly(2));
        }

        private static bool ExpressionContainsCommandWithContest(Expression<Func<MatchupPreviewProcessor, Task>> expr, Guid contestId)
        {
            if (expr.Body is MethodCallExpression call &&
                call.Arguments.FirstOrDefault() is UnaryExpression unary &&
                unary.Operand is ConstantExpression constant &&
                constant.Value is GenerateMatchupPreviewsCommand cmd)
            {
                return cmd.ContestId == contestId;
            }

            return false;
        }
    }
}
