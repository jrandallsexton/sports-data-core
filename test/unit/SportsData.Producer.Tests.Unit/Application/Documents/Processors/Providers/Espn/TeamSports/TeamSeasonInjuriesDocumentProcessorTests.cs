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
public class TeamSeasonInjuriesDocumentProcessorTests : ProducerTestBase<TeamSeasonInjuriesDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task ProcessAsync_DeserializesDto_Successfully()
    {
        // Arrange
        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonInjury.json");
        var dto = documentJson.FromJson<EspnTeamSeasonInjuryDto>();

        // Assert - Verify DTO deserialization
        dto.Should().NotBeNull();
        dto!.Id.Should().Be("171189");
        dto.Ref.Should().NotBeNull();
        dto.Ref.ToString().Should().Contain("athletes/4686093/injuries/171189");
        dto.Status.Should().Be("Active");
        dto.Date.Should().Be(new DateTime(2023, 1, 6, 18, 8, 0, DateTimeKind.Utc));
        
        // Verify headline/text extraction
        dto.GetHeadlineText().Should().Be("Williams announced Friday via his personal Twitter account that he's committed to transfer to Indiana.");
        dto.GetBodyText().Should().Contain("Williams is now set to join the Hoosiers");
        
        // Verify type
        dto.Type.Should().NotBeNull();
        dto.Type!.Id.Should().Be("0");
        dto.Type.Name.Should().Be("INJURY_STATUS_ACTIVE");
        dto.GetTypeName().Should().Be("INJURY_STATUS_ACTIVE");
        
        // Verify source
        dto.Source.Should().NotBeNull();
        dto.Source!.Id.Should().Be("1");
        dto.Source.Description.Should().Be("basic/manual");
        dto.GetSourceName().Should().Be("basic/manual");
        
        // Verify athlete reference
        dto.Athlete.Should().NotBeNull();
        dto.Athlete!.Ref.Should().NotBeNull();
        dto.Athlete.Ref.ToString().Should().Contain("athletes/4686093");
    }

    [Fact]
    public async Task ProcessAsync_CreatesAthleteSeasonInjury_WhenInjuryDoesNotExist()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Create athlete season
        var athleteSeasonRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093");
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

        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonInjury.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonInjuries)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2022)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonInjuriesDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var injuries = await FootballDataContext.AthleteSeasonInjuries.ToListAsync();
        injuries.Should().HaveCount(1);

        var injury = injuries.First();
        injury.AthleteSeasonId.Should().Be(athleteSeason.Id);
        injury.TypeId.Should().Be("0");
        injury.Type.Should().Be("INJURY_STATUS_ACTIVE");
        injury.TypeDescription.Should().Be("active");
        injury.TypeAbbreviation.Should().Be("A");
        injury.Headline.Should().Be("Williams announced Friday via his personal Twitter account that he's committed to transfer to Indiana.");
        injury.Text.Should().Contain("Williams is now set to join the Hoosiers");
        injury.Source.Should().Be("basic/manual");
        injury.Status.Should().Be("Active");
        injury.Date.Should().Be(new DateTime(2023, 1, 6, 18, 8, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ProcessAsync_UpdatesAthleteSeasonInjury_WhenInjuryExists()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Create athlete season
        var athleteSeasonRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093");
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

        // Create existing injury
        var injuryRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093/injuries/171189");
        var injuryIdentity = identityGenerator.Generate(injuryRef);
        
        var existingInjury = new AthleteSeasonInjury
        {
            Id = injuryIdentity.CanonicalId,
            AthleteSeasonId = athleteSeason.Id,
            TypeId = "99",
            Type = "OLD_TYPE",
            TypeDescription = "old description",
            TypeAbbreviation = "O",
            Date = DateTime.UtcNow.AddDays(-1),
            Headline = "Old headline",
            Text = "Old text",
            Source = "old source",
            Status = "Inactive",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.AthleteSeasonInjuries.AddAsync(existingInjury);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonInjury.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonInjuries)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2022)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonInjuriesDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var injuries = await FootballDataContext.AthleteSeasonInjuries.ToListAsync();
        injuries.Should().HaveCount(1);

        var injury = injuries.First();
        injury.Id.Should().Be(existingInjury.Id); // Same ID
        injury.TypeId.Should().Be("0"); // Updated
        injury.Type.Should().Be("INJURY_STATUS_ACTIVE"); // Updated
        injury.TypeDescription.Should().Be("active"); // Updated
        injury.TypeAbbreviation.Should().Be("A"); // Updated
        injury.Headline.Should().Be("Williams announced Friday via his personal Twitter account that he's committed to transfer to Indiana."); // Updated
        injury.Text.Should().Contain("Williams is now set to join the Hoosiers"); // Updated
        injury.Source.Should().Be("basic/manual"); // Updated
        injury.Status.Should().Be("Active"); // Updated
        injury.Date.Should().Be(new DateTime(2023, 1, 6, 18, 8, 0, DateTimeKind.Utc)); // Updated
    }

    [Fact]
    public async Task ProcessAsync_DoesNotUpdate_WhenNoChangesDetected()
    {
        // Arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

        // Create athlete season
        var athleteSeasonRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093");
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

        // Create existing injury with same values as JSON
        var injuryRef = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093/injuries/171189");
        var injuryIdentity = identityGenerator.Generate(injuryRef);
        
        var modifiedUtc = DateTime.UtcNow.AddMinutes(-5);
        var existingInjury = new AthleteSeasonInjury
        {
            Id = injuryIdentity.CanonicalId,
            AthleteSeasonId = athleteSeason.Id,
            TypeId = "0",
            Type = "INJURY_STATUS_ACTIVE",
            TypeDescription = "active",
            TypeAbbreviation = "A",
            Date = new DateTime(2023, 1, 6, 18, 8, 0, DateTimeKind.Utc),
            Headline = "Williams announced Friday via his personal Twitter account that he's committed to transfer to Indiana.",
            Text = "Williams is now set to join the Hoosiers next fall following a three-year stint at Clemson. The 6-foot-3 wide receiver has failed to surpass 10 receptions in both of the previous two campaigns, so he'll look to compete for a prominent role at his new university.",
            Source = "basic/manual",
            Status = "Active",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ModifiedUtc = modifiedUtc,
            ModifiedBy = Guid.NewGuid()
        };

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.AthleteSeasonInjuries.AddAsync(existingInjury);
        await FootballDataContext.SaveChangesAsync();

        var documentJson = await LoadJsonTestData("EspnFootballNcaaTeamSeasonInjury.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.DocumentType, DocumentType.TeamSeasonInjuries)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2022)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        var sut = Mocker.CreateInstance<TeamSeasonInjuriesDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var injury = await FootballDataContext.AthleteSeasonInjuries.FirstAsync();
        injury.ModifiedUtc.Should().Be(modifiedUtc); // Should NOT be updated
    }
}
