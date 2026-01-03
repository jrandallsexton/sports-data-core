using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMediaRefresh;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

public class EnqueueCompetitionMediaRefreshCommandHandlerTests : ProducerTestBase<EnqueueCompetitionMediaRefreshCommandHandler>
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_ReturnsAcceptedWithCompetitionId(bool removeExisting)
    {
        // Arrange
        var competitionId = Guid.NewGuid();
        var command = new EnqueueCompetitionMediaRefreshCommand(competitionId, removeExisting);

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var sut = Mocker.CreateInstance<EnqueueCompetitionMediaRefreshCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.Value.Should().Be(competitionId);
        result.Status.Should().Be(ResultStatus.Accepted);
    }
}
