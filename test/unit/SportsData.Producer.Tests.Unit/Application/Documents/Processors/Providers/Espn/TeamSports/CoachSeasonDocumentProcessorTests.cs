using AutoFixture;

using FluentAssertions;

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
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

[Collection("Sequential")]
public class CoachSeasonDocumentProcessorTests : ProducerTestBase<CoachSeasonDocumentProcessor<FootballDataContext>>
{
    private const string TestDataFile = "EspnFootballNcaaTeamSeasonCoach.json";

    private async Task<Guid> SeedFranchiseSeasonAsync()
    {
        // Indiana from test data
        var teamRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/84?lang=en&region=us");
        var identityGenerator = new ExternalRefIdentityGenerator();
        var franchiseSeasonIdentity = identityGenerator.Generate(teamRef);

        var franchiseSeason = new FranchiseSeason
        {
            Id = franchiseSeasonIdentity.CanonicalId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2025,
            Slug = "indiana-hoosiers-2025",
            Location = "Indiana",
            Name = "Hoosiers",
            Abbreviation = "IND",
            DisplayName = "Indiana Hoosiers",
            DisplayNameShort = "Indiana",
            ColorCodeHex = "#990000",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();
        return franchiseSeason.Id;
    }

    private async Task<Guid> SeedCoachAsync()
    {
        // Curt Cignetti from test data
        var personRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/4079704?lang=en&region=us");
        var identityGenerator = new ExternalRefIdentityGenerator();
        var personIdentity = identityGenerator.Generate(personRef);

        var coach = new Coach
        {
            Id = personIdentity.CanonicalId,
            FirstName = "Curt",
            LastName = "Cignetti",
            Experience = 15,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Coaches.AddAsync(coach);
        await FootballDataContext.SaveChangesAsync();
        return coach.Id;
    }

    [Fact]
    public async Task ProcessAsync_CreatesNewCoachSeason_WithRealTestData()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = await SeedFranchiseSeasonAsync();
        var coachId = await SeedCoachAsync();

        var documentJson = await LoadJsonTestData(TestDataFile);
        var dto = documentJson.FromJson<EspnCoachSeasonDto>();
        var coachSeasonIdentity = identityGenerator.Generate(dto!.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.CoachSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<CoachSeasonDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - verify CoachSeason was created
        var coachSeason = await FootballDataContext.CoachSeasons
            .FirstOrDefaultAsync(x => x.Id == coachSeasonIdentity.CanonicalId);

        coachSeason.Should().NotBeNull();
        coachSeason!.CoachId.Should().Be(coachId);
        coachSeason.FranchiseSeasonId.Should().Be(franchiseSeasonId);
        coachSeason.Title.Should().Be("Cignetti");
        coachSeason.IsActive.Should().BeTrue();

        // Assert - verify CoachSeasonRecord request was published
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.CoachSeasonRecord &&
                    e.ParentId == coachSeasonIdentity.CanonicalId.ToString() &&
                    e.Uri.ToString().Contains("coaches/4079704/record")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should request CoachSeasonRecord document");
    }

    [Fact]
    public async Task ProcessAsync_UpdatesExistingCoachSeason_WhenAlreadyExists()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = await SeedFranchiseSeasonAsync();
        var coachId = await SeedCoachAsync();

        var documentJson = await LoadJsonTestData(TestDataFile);
        var dto = documentJson.FromJson<EspnCoachSeasonDto>();
        var coachSeasonIdentity = identityGenerator.Generate(dto!.Ref);

        // Create existing inactive CoachSeason
        var existingCoachSeason = new CoachSeason
        {
            Id = coachSeasonIdentity.CanonicalId,
            CoachId = coachId,
            FranchiseSeasonId = franchiseSeasonId,
            Title = "Old Title",
            IsActive = false,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.CoachSeasons.AddAsync(existingCoachSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.CoachSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<CoachSeasonDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - existing CoachSeason should be reactivated and title updated
        var updatedCoachSeason = await FootballDataContext.CoachSeasons
            .FirstOrDefaultAsync(x => x.Id == coachSeasonIdentity.CanonicalId);

        updatedCoachSeason.Should().NotBeNull();
        updatedCoachSeason!.IsActive.Should().BeTrue("existing coach should be activated");
        updatedCoachSeason.Title.Should().Be("Cignetti", "title should be updated");
        updatedCoachSeason.ModifiedUtc.Should().NotBeNull("ModifiedUtc should be set");
    }

    [Fact]
    public async Task ProcessAsync_PublishesRetryEvent_WhenCoachMissing()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = await SeedFranchiseSeasonAsync();
        // Don't seed the Coach - it's missing

        var documentJson = await LoadJsonTestData(TestDataFile);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.CoachSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.AttemptCount, 1)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<CoachSeasonDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - should request Coach document sourcing
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.Coach &&
                    e.Uri.ToString().Contains("coaches/4079704") &&
                    !e.Uri.ToString().Contains("seasons")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should request Coach document sourcing");

        // Should also publish retry event with headers
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentCreated>(e => e.AttemptCount == 2),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should publish retry event");
    }

    [Fact]
    public async Task ProcessAsync_PublishesRetryEvent_WhenFranchiseSeasonMissing()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        await SeedCoachAsync();
        // Don't seed FranchiseSeason - it's missing

        var documentJson = await LoadJsonTestData(TestDataFile);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.CoachSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.AttemptCount, 1)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<CoachSeasonDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - should request FranchiseSeason document sourcing
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.TeamSeason &&
                    e.Uri.ToString().Contains("teams/84")),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should request TeamSeason document sourcing");

        // Should also publish retry event with headers
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentCreated>(e => e.AttemptCount == 2),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once,
            "should publish retry event");
    }

    [Fact]
    public async Task ProcessAsync_DoesNotPublishRecordRequests_WhenRecordsCollectionEmpty()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        var franchiseSeasonId = await SeedFranchiseSeasonAsync();
        var coachId = await SeedCoachAsync();

        var dto = new EspnCoachSeasonDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/4079704"),
            Id = "4079704",
            Uid = "s:20~l:23~co:4079704",
            FirstName = "Curt",
            LastName = "Cignetti",
            Person = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/coaches/4079704") },
            Team = new EspnLinkDto { Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/teams/84") },
            Records = new List<EspnCoachSeasonRecordsDto>() // Empty records
        };

        var documentJson = dto.ToJson();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.CoachSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<CoachSeasonDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert - should NOT request CoachSeasonRecord
        bus.Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.CoachSeasonRecord),
                It.IsAny<CancellationToken>()),
            Times.Never,
            "should not request CoachSeasonRecord when records collection is empty");
    }
}

