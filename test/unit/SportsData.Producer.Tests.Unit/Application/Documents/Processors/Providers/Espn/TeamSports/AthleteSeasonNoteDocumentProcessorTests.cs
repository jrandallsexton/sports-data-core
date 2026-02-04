using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

[Collection("Sequential")]
public class AthleteSeasonNoteDocumentProcessorTests : ProducerTestBase<AthleteSeasonNoteDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task ProcessAsync_DeserializesDto_Successfully()
    {
        // Arrange
        var documentJson = await LoadJsonTestData("EspnFootballAthleteSeasonNotes.json");
        var dto = documentJson.FromJson<EspnAthleteSeasonNoteDto>();

        // Assert - Verify DTO deserialization
        dto.Should().NotBeNull();
        dto!.Id.Should().Be("13062586");
        dto.Ref.Should().NotBeNull();
        dto.Ref.ToString().Should().Contain("notes/13062586");
        dto.Type.Should().Be("news");
        dto.Date.Should().Be(new DateTime(2024, 12, 13, 2, 40, 6, DateTimeKind.Utc));
        
        // Verify headline/text extraction
        dto.GetHeadlineText().Should().Be("Koi Perich receives 2024 Big Ten Freshman of the Year honor");
        dto.GetBodyText().Should().Contain("Perich recorded a team-high five interceptions");
        
        // Verify type
        dto.GetTypeName().Should().Be("news");
        
        // Verify source
        dto.Source.Should().Be("RotoWire");
        
        // Verify athlete reference
        dto.Athlete.Should().NotBeNull();
        dto.Athlete!.Ref.Should().NotBeNull();
        dto.Athlete.Ref.ToString().Should().Contain("athletes/5156369");
    }

    [Fact]
    public async Task ProcessAsync_CreatesAthleteSeasonNote_WhenNoteDoesNotExist()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Create athlete season
        var athleteSeasonRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/5156369");
        var athleteSeasonIdentity = identityGenerator.Generate(athleteSeasonRef);
        
        var athleteSeason = new FootballAthleteSeason
        {
            Id = athleteSeasonIdentity.CanonicalId,
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData("EspnFootballAthleteSeasonNotes.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonNote)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<AthleteSeasonNoteDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var notes = await FootballDataContext.AthleteSeasonNotes.ToListAsync();
        notes.Should().HaveCount(1);

        var note = notes.First();
        note.AthleteSeasonId.Should().Be(athleteSeason.Id);
        note.Type.Should().Be("news");
        note.Headline.Should().Be("Koi Perich receives 2024 Big Ten Freshman of the Year honor");
        note.Text.Should().Contain("Perich recorded a team-high five interceptions");
        note.Source.Should().Be("RotoWire");
        note.Date.Should().Be(new DateTime(2024, 12, 13, 2, 40, 6, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ProcessAsync_UpdatesAthleteSeasonNote_WhenNoteExists()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Create athlete season
        var athleteSeasonRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/5156369");
        var athleteSeasonIdentity = identityGenerator.Generate(athleteSeasonRef);
        
        var athleteSeason = new FootballAthleteSeason
        {
            Id = athleteSeasonIdentity.CanonicalId,
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = Guid.NewGuid()
        };

        // Create existing note
        var noteRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/notes/13062586");
        var noteIdentity = identityGenerator.Generate(noteRef);
        
        var existingNote = new AthleteSeasonNote
        {
            Id = noteIdentity.CanonicalId,
            AthleteSeasonId = athleteSeason.Id,
            Type = "old_type",
            Headline = "Old headline",
            Text = "Old text",
            Source = "OldSource",
            Date = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = DateTime.UtcNow.AddDays(-10),
            ModifiedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.AthleteSeasonNotes.AddAsync(existingNote);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData("EspnFootballAthleteSeasonNotes.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonNote)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<AthleteSeasonNoteDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var notes = await FootballDataContext.AthleteSeasonNotes.ToListAsync();
        notes.Should().HaveCount(1);

        var note = notes.First();
        note.Id.Should().Be(existingNote.Id);
        note.AthleteSeasonId.Should().Be(athleteSeason.Id);
        note.Type.Should().Be("news");
        note.Headline.Should().Be("Koi Perich receives 2024 Big Ten Freshman of the Year honor");
        note.Text.Should().Contain("Perich recorded a team-high five interceptions");
        note.Source.Should().Be("RotoWire");
        note.Date.Should().Be(new DateTime(2024, 12, 13, 2, 40, 6, DateTimeKind.Utc));
        note.ModifiedBy.Should().Be(command.CorrelationId);
    }

    [Fact]
    public async Task ProcessAsync_DoesNotUpdate_WhenNoChangesDetected()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Create athlete season
        var athleteSeasonRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/5156369");
        var athleteSeasonIdentity = identityGenerator.Generate(athleteSeasonRef);
        
        var athleteSeason = new FootballAthleteSeason
        {
            Id = athleteSeasonIdentity.CanonicalId,
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = Guid.NewGuid()
        };

        // Create existing note with same values as incoming data
        var noteRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/notes/13062586");
        var noteIdentity = identityGenerator.Generate(noteRef);
        
        var originalModifiedUtc = DateTime.UtcNow.AddDays(-10);
        var originalModifiedBy = Guid.NewGuid();
        
        var existingNote = new AthleteSeasonNote
        {
            Id = noteIdentity.CanonicalId,
            AthleteSeasonId = athleteSeason.Id,
            Type = "news",
            Headline = "Koi Perich receives 2024 Big Ten Freshman of the Year honor",
            Text = "Perich recorded a team-high five interceptions in his debut season for the Golden Gophers, becoming the first freshman to earn this honor since 2016. The 6-0, 185-pound defensive back also tallied 52 tackles, three pass deflections and one defensive touchdown en route to being named first-team All-Big Ten by the conference's coaches. Perich is a legitimate candidate to crack the first round of the 2027 NFL Draft.",
            Source = "RotoWire",
            Date = new DateTime(2024, 12, 13, 2, 40, 6, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = originalModifiedUtc,
            ModifiedBy = originalModifiedBy
        };

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.AthleteSeasonNotes.AddAsync(existingNote);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData("EspnFootballAthleteSeasonNotes.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.AthleteSeasonNote)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<AthleteSeasonNoteDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var notes = await FootballDataContext.AthleteSeasonNotes.ToListAsync();
        notes.Should().HaveCount(1);

        var note = notes.First();
        note.ModifiedUtc.Should().Be(originalModifiedUtc);
        note.ModifiedBy.Should().Be(originalModifiedBy);
    }
}
