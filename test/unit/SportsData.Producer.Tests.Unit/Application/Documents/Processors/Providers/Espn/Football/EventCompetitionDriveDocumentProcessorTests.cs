#nullable enable

using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class EventCompetitionDriveDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private const string DriveUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/drives/4016283341?lang=en";

    private ProcessDocumentCommand CreateCommand(string jsonFile, string? parentId = null)
    {
        var generator = new ExternalRefIdentityGenerator();
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, jsonFile)
            .With(x => x.DocumentType, DocumentType.EventCompetitionDrive)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, parentId ?? Guid.NewGuid().ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.UrlHash, generator.Generate(DriveUrl).UrlHash)
            .Create();
    }

    [Fact]
    public async Task WhenEntityDoesNotExist_ShouldCreateDriveWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        var competition = Fixture.Build<Competition>()
            .OmitAutoProperties()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, Guid.NewGuid())
            .With(x => x.CreatedBy, Guid.NewGuid())
            .With(x => x.Drives, new List<Drive>())
            .Create();
        await FootballDataContext.Competitions.AddAsync(competition);

        var startTeamId = Guid.NewGuid();
        var startFranchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, startTeamId)
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .With(x => x.Abbreviation, "START")
            .With(x => x.ColorCodeHex, "#FFFFFF")
            .With(x => x.DisplayName, "Start Team")
            .With(x => x.DisplayNameShort, "DisplayNameShort")
            .With(x => x.Location, "Location")
            .With(x => x.Name, "Start Team Name")
            .With(x => x.Slug, "start-team")
            .Create();
        
        var teamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en";
        startFranchiseSeason.ExternalIds = new List<FranchiseSeasonExternalId>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = generator.Generate(teamUrl).UrlHash,
                SourceUrlHash = generator.Generate(teamUrl).UrlHash,
                SourceUrl = teamUrl
            }
        };
        await FootballDataContext.FranchiseSeasons.AddAsync(startFranchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionDriveDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionDrive.json");
        
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var created = await FootballDataContext.Drives
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync();

        created.Should().NotBeNull();
        created!.CompetitionId.Should().Be(competitionId);
        created.StartFranchiseSeasonId.Should().NotBeNull();
        created.Description.Should().Be("13 plays, 74 yards, 7:14");
        created.SequenceNumber.Should().Be("1");
        created.ExternalIds.Should().NotBeEmpty();
        created.ExternalIds.Should().ContainSingle(x => 
            x.Provider == SourceDataProvider.Espn && 
            x.SourceUrlHash == generator.Generate(DriveUrl).UrlHash);
        created.CreatedBy.Should().Be(command.CorrelationId);
    }

    [Fact]
    public async Task WhenEntityAlreadyExists_ShouldSkipCreation()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        var competition = Fixture.Build<Competition>()
            .OmitAutoProperties()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, Guid.NewGuid())
            .With(x => x.CreatedBy, Guid.NewGuid())
            .With(x => x.Drives, new List<Drive>())
            .Create();
        await FootballDataContext.Competitions.AddAsync(competition);

        var startTeamId = Guid.NewGuid();
        var startFranchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, startTeamId)
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .With(x => x.Abbreviation, "START")
            .With(x => x.ColorCodeHex, "#FFFFFF")
            .With(x => x.DisplayName, "Start Team")
            .With(x => x.DisplayNameShort, "DisplayNameShort")
            .With(x => x.Location, "Location")
            .With(x => x.Name, "Start Team Name")
            .With(x => x.Slug, "start-team")
            .Create();

        var teamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en";
        startFranchiseSeason.ExternalIds = new List<FranchiseSeasonExternalId>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = generator.Generate(teamUrl).UrlHash,
                SourceUrlHash = generator.Generate(teamUrl).UrlHash,
                SourceUrl = teamUrl
            }
        };
        await FootballDataContext.FranchiseSeasons.AddAsync(startFranchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var correlationId = Guid.NewGuid();
        var existingDrive = Fixture.Build<Drive>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Description, "Existing drive")
            .With(x => x.SequenceNumber, "1")
            .With(x => x.Ordinal, 1)
            .With(x => x.CompetitionId, Guid.NewGuid())
            .With(x => x.CreatedBy, correlationId)
            .Create();

        var driveIdentity = generator.Generate(DriveUrl);
        existingDrive.ExternalIds = new List<DriveExternalId>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                Value = driveIdentity.UrlHash,
                SourceUrlHash = driveIdentity.UrlHash,
                SourceUrl = DriveUrl,
                DriveId = existingDrive.Id
            }
        };
        
        await FootballDataContext.Drives.AddAsync(existingDrive);
        await FootballDataContext.SaveChangesAsync();

        var initialCount = await FootballDataContext.Drives.CountAsync();
        
        var sut = Mocker.CreateInstance<EventCompetitionDriveDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionDrive.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var finalCount = await FootballDataContext.Drives.CountAsync();
        finalCount.Should().Be(initialCount);
    }

    [Fact]
    public async Task WhenInvalidParentId_ShouldThrowException()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var sut = Mocker.CreateInstance<EventCompetitionDriveDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionDrive.json");
        var command = CreateCommand(json, "not-a-guid");

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => sut.ProcessAsync(command));
    }

    [Fact(Skip = "No longer valid, but might want to revisit")]
    public async Task WhenStartTeamNotFound_ShouldThrowException()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competition = Fixture.Build<Competition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, Guid.NewGuid())
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionDriveDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionDrive.json");
        var command = CreateCommand(json, competition.Id.ToString());

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await sut.ProcessAsync(command));
    }
}