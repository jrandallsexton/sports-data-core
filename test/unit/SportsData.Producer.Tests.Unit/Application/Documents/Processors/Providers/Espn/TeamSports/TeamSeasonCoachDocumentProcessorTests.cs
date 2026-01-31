using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

[Collection("Sequential")]
public class TeamSeasonCoachDocumentProcessorTests : ProducerTestBase<TeamSeasonCoachDocumentProcessor<TeamSportDataContext>>
{
    private async Task SeedFranchiseSeasonAsync(Guid franchiseSeasonId)
    {
        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2025,
            Slug = "lsu-tigers-2025",
            Location = "LSU",
            Name = "Tigers",
            Abbreviation = "LSU",
            DisplayName = "LSU Tigers",
            DisplayNameShort = "LSU",
            ColorCodeHex = "#461D7C",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task SeedCoachSeasonsAsync(Guid franchiseSeasonId, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var coach = new Coach
            {
                Id = Guid.NewGuid(),
                FirstName = $"Coach{i}",
                LastName = $"LastName{i}",
                Experience = i,
                CreatedUtc = DateTime.UtcNow
            };

            await FootballDataContext.Coaches.AddAsync(coach);

            var coachSeason = new CoachSeason
            {
                Id = Guid.NewGuid(),
                CoachId = coach.Id,
                FranchiseSeasonId = franchiseSeasonId,
                Title = $"Coach{i} Title",
                IsActive = true,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.CoachSeasons.AddAsync(coachSeason);
        }

        await FootballDataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task ProcessAsync_UpdatesExistingCoachSeason_WhenCoachSeasonAlreadyExists()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();
        await SeedFranchiseSeasonAsync(franchiseSeasonId);

        // Create existing inactive coach season
        var coachRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/559872");
        var personRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872");
        var coachSeasonIdentity = identityGenerator.Generate(coachRef);
        var personIdentity = identityGenerator.Generate(personRef);

        var coach = new Coach
        {
            Id = personIdentity.CanonicalId,
            FirstName = "Brian",
            LastName = "Kelly",
            Experience = 25,
            CreatedUtc = DateTime.UtcNow
        };
        await FootballDataContext.Coaches.AddAsync(coach);

        var existingCoachSeason = new CoachSeason
        {
            Id = coachSeasonIdentity.CanonicalId,
            CoachId = coach.Id,
            FranchiseSeasonId = franchiseSeasonId,
            Title = "Head Coach",
            IsActive = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CoachSeasons.AddAsync(existingCoachSeason);
        await FootballDataContext.SaveChangesAsync();

        // Create single coach document
        var coachDto = new EspnCoachSeasonDto
        {
            Ref = coachRef,
            Id = "559872",
            Uid = "s:20~l:23~co:559872",
            FirstName = "Brian",
            LastName = "Kelly",
            Experience = 25,
            Person = new EspnLinkDto { Ref = personRef }
        };

        var documentJson = coachDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonCoach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonCoachDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - existing coach should be reactivated
        var updatedCoach = await FootballDataContext.CoachSeasons
            .FirstOrDefaultAsync(x => x.Id == coachSeasonIdentity.CanonicalId);

        updatedCoach.Should().NotBeNull();
        updatedCoach!.IsActive.Should().BeTrue("existing coach should be activated");
        updatedCoach.ModifiedUtc.Should().NotBeNull("ModifiedUtc should be set");
    }

    [Fact]
    public async Task ProcessAsync_SpawnsChildDocument_ForSingleCoach()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();
        await SeedFranchiseSeasonAsync(franchiseSeasonId);

        // Seed the Person (Coach) first
        var coachRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/559872");
        var personRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/559872");
        var personIdentity = identityGenerator.Generate(personRef);

        var coach = new Coach
        {
            Id = personIdentity.CanonicalId,
            FirstName = "Brian",
            LastName = "Kelly",
            Experience = 25,
            CreatedUtc = DateTime.UtcNow
        };
        await FootballDataContext.Coaches.AddAsync(coach);
        await FootballDataContext.SaveChangesAsync();

        var coachDto = new EspnCoachSeasonDto
        {
            Ref = coachRef,
            Id = "559872",
            Uid = "s:20~l:23~co:559872",
            FirstName = "Brian",
            LastName = "Kelly",
            Experience = 25,
            Person = new EspnLinkDto { Ref = personRef }
        };

        var documentJson = coachDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonCoach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<TeamSeasonCoachDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - verify child document request was published
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.CoachSeason &&
                    e.ParentId == franchiseSeasonId.ToString()),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotAffectOtherCoaches_WhenProcessingSingleCoach()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();
        await SeedFranchiseSeasonAsync(franchiseSeasonId);

        // Seed multiple existing coaches
        await SeedCoachSeasonsAsync(franchiseSeasonId, 3);

        var initialActiveCount = await FootballDataContext.CoachSeasons
            .CountAsync(x => x.FranchiseSeasonId == franchiseSeasonId && x.IsActive);
        initialActiveCount.Should().Be(3, "all seeded coaches should start active");

        // Seed the Person (Coach) for the new coach
        var newCoachRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/999999");
        var personRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/999999");
        var personIdentity = identityGenerator.Generate(personRef);

        var coach = new Coach
        {
            Id = personIdentity.CanonicalId,
            FirstName = "New",
            LastName = "Coach",
            Experience = 5,
            CreatedUtc = DateTime.UtcNow
        };
        await FootballDataContext.Coaches.AddAsync(coach);
        await FootballDataContext.SaveChangesAsync();

        // Process a new coach (not one of the existing three)
        var coachDto = new EspnCoachSeasonDto
        {
            Ref = newCoachRef,
            Id = "999999",
            Uid = "s:20~l:23~co:999999",
            FirstName = "New",
            LastName = "Coach",
            Experience = 5,
            Person = new EspnLinkDto { Ref = personRef }
        };

        var documentJson = coachDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonCoach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonCoachDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - existing coaches should remain unchanged
        var finalActiveCount = await FootballDataContext.CoachSeasons
            .CountAsync(x => x.FranchiseSeasonId == franchiseSeasonId && x.IsActive);
        finalActiveCount.Should().Be(3, "existing coaches should not be affected");

        // Assert - new coach document request should have been published
        Mocker.GetMock<IPublishEndpoint>()
            .Verify(x => x.Publish(
                It.Is<DocumentRequested>(d =>
                    d.DocumentType == DocumentType.CoachSeason &&
                    d.Uri.ToString().Contains("999999")),
                It.IsAny<CancellationToken>()),
            Times.Once, "new coach document should be requested for processing");
    }

    [Fact]
    public async Task ProcessAsync_PublishesDocumentRequestedAndEmitsRetryDocumentCreated_WhenPersonDocumentMissing()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = Guid.NewGuid();
        await SeedFranchiseSeasonAsync(franchiseSeasonId);

        var coachRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/123456");
        var personRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/123456");

        // Don't seed the Person (Coach) - it's missing
        var coachDto = new EspnCoachSeasonDto
        {
            Ref = coachRef,
            Id = "123456",
            Uid = "s:20~l:23~co:123456",
            FirstName = "Missing",
            LastName = "Person",
            Experience = 10,
            Person = new EspnLinkDto { Ref = personRef }
        };

        var documentJson = coachDto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonCoach)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, franchiseSeasonId.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.AttemptCount, 1)
            .Create();

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<TeamSeasonCoachDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - should request Person document
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.Coach &&
                    e.Uri.ToString().Contains("coaches/123456") &&
                    !e.Uri.ToString().Contains("seasons")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should request Person document sourcing");

        // Should also publish retry event
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentCreated>(e => e.AttemptCount == 2),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should publish retry event");
    }
}

