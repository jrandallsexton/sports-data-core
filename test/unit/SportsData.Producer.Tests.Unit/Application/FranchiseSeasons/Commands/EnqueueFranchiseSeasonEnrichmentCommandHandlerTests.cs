using System.Linq.Expressions;

using AutoFixture;

using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueFranchiseSeasonEnrichment;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Commands;

public class EnqueueFranchiseSeasonEnrichmentCommandHandlerTests :
    ProducerTestBase<EnqueueFranchiseSeasonEnrichmentCommandHandler>
{
    public EnqueueFranchiseSeasonEnrichmentCommandHandlerTests()
    {
        // Register the validator
        Mocker.Use<IValidator<EnqueueFranchiseSeasonEnrichmentCommand>>(
            new EnqueueFranchiseSeasonEnrichmentCommandValidator());
    }

    [Fact]
    public async Task WhenFranchiseSeasonsExist_ShouldEnqueueEnrichJobsForEach()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonEnrichmentCommandHandler>();

        var sport = Sport.FootballNcaa;
        var seasonYear = 2024;

        // Create franchise seasons for target sport/year
        for (int i = 0; i < 5; i++)
        {
            var franchise = CreateFranchise(sport);
            var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);
            await FootballDataContext.Franchises.AddAsync(franchise);
            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new EnqueueFranchiseSeasonEnrichmentCommand(seasonYear, sport);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        result.Value.Should().NotBeEmpty();

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()),
            Times.Exactly(5));
    }

    [Fact]
    public async Task WhenNoFranchiseSeasonsExist_ShouldReturnSuccessWithNoEnqueues()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonEnrichmentCommandHandler>();

        var sport = Sport.FootballNcaa;
        var seasonYear = 2024;
        var command = new EnqueueFranchiseSeasonEnrichmentCommand(seasonYear, sport);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenFranchiseSeasonsExistForDifferentSportOrSeason_ShouldNotEnqueueThoseSeasons()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonEnrichmentCommandHandler>();

        var targetSport = Sport.FootballNcaa;
        var targetSeasonYear = 2024;

        // Create franchise seasons for target sport/season
        for (int i = 0; i < 2; i++)
        {
            var franchise = CreateFranchise(targetSport);
            var franchiseSeason = CreateFranchiseSeason(franchise.Id, targetSeasonYear);
            await FootballDataContext.Franchises.AddAsync(franchise);
            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }

        // Create franchise seasons for different season
        for (int i = 0; i < 3; i++)
        {
            var franchise = CreateFranchise(targetSport);
            var franchiseSeason = CreateFranchiseSeason(franchise.Id, 2025);
            await FootballDataContext.Franchises.AddAsync(franchise);
            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }

        await FootballDataContext.SaveChangesAsync();

        var command = new EnqueueFranchiseSeasonEnrichmentCommand(targetSeasonYear, targetSport);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<Guid>>();
        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task WhenValidationFails_ShouldReturnFailure()
    {
        // Arrange
        var backgroundJobProvider = Mocker.GetMock<IProvideBackgroundJobs>();
        var sut = Mocker.CreateInstance<EnqueueFranchiseSeasonEnrichmentCommandHandler>();

        var command = new EnqueueFranchiseSeasonEnrichmentCommand(
            SeasonYear: 1999, // Invalid: before 2000
            Sport: Sport.FootballNcaa);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Failure<Guid>>();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);

        var failure = result as Failure<Guid>;
        failure!.Errors.Should().NotBeEmpty();

        backgroundJobProvider.Verify(
            x => x.Enqueue(It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()),
            Times.Never);
    }

    private Franchise CreateFranchise(Sport sport)
    {
        return Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Franchise")
            .With(x => x.DisplayName, "Test Franchise")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, "test-franchise")
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Sport, sport)
            .Create();
    }

    private FranchiseSeason CreateFranchiseSeason(Guid franchiseId, int seasonYear)
    {
        return Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchiseId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Name, "Test Franchise Season")
            .With(x => x.DisplayName, "Test Franchise Season")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Abbreviation, "TEST")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, "test-franchise-season")
            .With(x => x.ColorCodeHex, "#000000")
            .Create();
    }
}
