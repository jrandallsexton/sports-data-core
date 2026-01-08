using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonMetricsGeneration;
using SportsData.Producer.Application.GroupSeasons;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Commands;

public class EnqueueFranchiseSeasonMetricsGenerationCommandHandlerTests :
    ProducerTestBase<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>
{
    [Fact]
    public async Task WhenFranchiseSeasonsExist_ShouldEnqueueJobsForEach()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var groupSeasonsService = Mocker.GetMock<IGroupSeasonsService>();

        var groupSeasonId = Guid.NewGuid();
        groupSeasonsService
            .Setup(x => x.GetFbsGroupSeasonIds(It.IsAny<int>()))
            .ReturnsAsync(new HashSet<Guid> { groupSeasonId });

        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();

        // Create franchises and franchise seasons
        for (int i = 0; i < 3; i++)
        {
            var franchise = Fixture.Build<Franchise>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Name, $"Team {i}")
                .With(x => x.Abbreviation, $"T{i}")
                .With(x => x.Location, $"Location {i}")
                .With(x => x.DisplayName, $"Location {i} Team {i}")
                .With(x => x.DisplayNameShort, $"Team {i}")
                .With(x => x.ColorCodeHex, "#000000")
                .With(x => x.Slug, $"team-{i}")
                .Create();

            await FootballDataContext.Franchises.AddAsync(franchise);

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.FranchiseId, franchise.Id)
                .With(x => x.Franchise, franchise)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.GroupSeasonId, groupSeasonId)
                .With(x => x.Slug, franchise.Slug)
                .With(x => x.Location, franchise.Location)
                .With(x => x.Name, franchise.Name)
                .With(x => x.Abbreviation, franchise.Abbreviation ?? "TM")
                .With(x => x.DisplayName, franchise.DisplayName)
                .With(x => x.DisplayNameShort, franchise.DisplayNameShort)
                .With(x => x.ColorCodeHex, franchise.ColorCodeHex)
                .Create();

            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new EnqueueFranchiseSeasonMetricsGenerationCommand(2024, Sport.FootballNcaa);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task WhenNoFranchiseSeasonsExist_ShouldReturnSuccessWithNoEnqueues()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var groupSeasonsService = Mocker.GetMock<IGroupSeasonsService>();

        groupSeasonsService
            .Setup(x => x.GetFbsGroupSeasonIds(It.IsAny<int>()))
            .ReturnsAsync(new HashSet<Guid> { Guid.NewGuid() });

        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonMetricsGenerationCommandHandler>();

        var command = new EnqueueFranchiseSeasonMetricsGenerationCommand(2024, Sport.FootballNcaa);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<ICalculateFranchiseSeasonMetricsCommandHandler, Task>>>()),
            Times.Never);
    }
}
