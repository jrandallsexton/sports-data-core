using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

[Collection("Sequential")]
public class AthleteCareerStatisticsDocumentProcessorTests :
    ProducerTestBase<AthleteCareerStatisticsDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task ProcessAsync_CreatesCareerStatistics_WhenAthleteExists()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteCareerStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNfl/EspnFootballNflAthleteCareerStatistics.json");
        var dto = json.FromJson<EspnAthleteCareerStatisticsDto>();

        // Derive the athlete ref from the career stats ref
        // The JSON athlete.$ref = http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/3915511
        var athleteRef = dto!.Athlete.Ref;
        var athleteIdentity = generator.Generate(athleteRef);

        // Seed an AthleteBase entity (Joe Burrow, id 3915511)
        var athlete = new FootballAthlete
        {
            Id = athleteIdentity.CanonicalId,
            FirstName = "Joe",
            LastName = "Burrow",
            DisplayName = "Joe Burrow",
            ShortName = "J. Burrow",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.SaveChangesAsync();

        var statsIdentity = generator.Generate(dto.Ref);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.DocumentType, DocumentType.AthleteCareerStatistics)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, statsIdentity.UrlHash)
            .With(x => x.ParentId, athlete.Id.ToString())
            .Without(x => x.SeasonYear)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var entity = await FootballDataContext.AthleteCareerStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .FirstOrDefaultAsync();

        entity.Should().NotBeNull();
        entity!.Id.Should().Be(statsIdentity.CanonicalId);
        entity.AthleteId.Should().Be(athlete.Id);
        entity.SplitId.Should().Be("0");
        entity.SplitName.Should().Be("All Splits");

        // The JSON has 8 categories with 205 total stats
        entity.Categories.Should().HaveCount(8);
        entity.Categories.Sum(c => c.Stats.Count).Should().Be(205);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsEarly_WhenAthleteMissing()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<AthleteCareerStatisticsDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNfl/EspnFootballNflAthleteCareerStatistics.json");
        var dto = json.FromJson<EspnAthleteCareerStatisticsDto>();
        var statsIdentity = generator.Generate(dto!.Ref);

        // Derive athlete ID so the ParentId resolves, but do NOT seed the AthleteBase entity
        var athleteRef = dto.Athlete.Ref;
        var athleteIdentity = generator.Generate(athleteRef);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNfl)
            .With(x => x.DocumentType, DocumentType.AthleteCareerStatistics)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, statsIdentity.UrlHash)
            .With(x => x.ParentId, athleteIdentity.CanonicalId.ToString())
            .Without(x => x.SeasonYear)
            .Create();

        // Act — processor logs error and returns early when athlete is not found
        await sut.ProcessAsync(command);

        // Assert — no career statistics should be created
        var stats = await FootballDataContext.AthleteCareerStatistics.ToListAsync();
        stats.Should().BeEmpty("processor should return early when athlete is not in the database");
    }
}
