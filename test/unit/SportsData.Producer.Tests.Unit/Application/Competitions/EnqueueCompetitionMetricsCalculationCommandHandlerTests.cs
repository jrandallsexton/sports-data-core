using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.EnqueueCompetitionMetricsCalculation;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions;

public class EnqueueCompetitionMetricsCalculationCommandHandlerTests : ProducerTestBase<EnqueueCompetitionMetricsCalculationCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ReturnsAcceptedWithCompetitionId()
    {
        // Arrange
        var competitionId = Guid.NewGuid();
        var command = new EnqueueCompetitionMetricsCalculationCommand(competitionId);

        var backgroundJobProvider = new Mock<IProvideBackgroundJobs>();
        Mocker.Use(backgroundJobProvider.Object);

        var sut = Mocker.CreateInstance<EnqueueCompetitionMetricsCalculationCommandHandler>();

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.Value.Should().Be(competitionId);
        result.Status.Should().Be(ResultStatus.Accepted);
    }
}
