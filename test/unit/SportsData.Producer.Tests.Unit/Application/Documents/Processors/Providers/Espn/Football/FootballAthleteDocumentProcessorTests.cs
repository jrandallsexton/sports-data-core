using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class FootballAthleteDocumentProcessorTests :
    ProducerTestBase<FootballAthleteDocumentProcessor<FootballDataContext>>
{

    [Fact]
    public void AsFootballAthlete_CapturesThrowingHand()
    {
        // The mapper previously dropped ESPN's `hand` (QB handedness).
        var dto = new EspnFootballAthleteDto
        {
            Ref = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/123"),
            Hand = new EspnAthleteHandDto { Type = "LEFT", Abbreviation = "L", DisplayValue = "Left" }
        };

        var entity = dto.AsFootballAthlete(
            new ExternalRefIdentityGenerator(),
            franchiseId: null,
            correlationId: Guid.NewGuid());

        entity.HandType.Should().Be("LEFT");
        entity.HandAbbreviation.Should().Be("L");
        entity.HandDisplayValue.Should().Be("Left");
    }

    [Fact]
    public async Task WhenAthleteIsValid_ShouldCreateAthlete()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var publishEndpoint = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<FootballAthleteDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaAthlete_Debug.json");
        var dto = json.FromJson<EspnFootballAthleteDto>();

        var dtoIdentity = generator.Generate(dto.Ref);

        var positionIdentity = generator.Generate(dto.Position.Ref);

        var position = Fixture.Build<AthletePosition>()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Abbreviation, "QB")
            .With(x => x.ExternalIds, new List<AthletePositionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = positionIdentity.CleanUrl,
                    SourceUrlHash = positionIdentity.UrlHash,
                    Value = positionIdentity.UrlHash
                }
            })
            .Create();

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.SeasonYear, 2025)
            .With(x => x.DocumentType, DocumentType.Athlete)
            .With(x => x.UrlHash, dtoIdentity.UrlHash)
            .With(x => x.Document, json)
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var athlete = await FootballDataContext.Athletes.FirstOrDefaultAsync();
        athlete.Should().NotBeNull();
        athlete!.FirstName.Should().Be(dto.FirstName);
        athlete.LastName.Should().Be(dto.LastName);
        athlete.PositionId.Should().Be(position.Id);
    }

    [Fact]
    public async Task WhenAthleteExists_Update_RefreshesHand()
    {
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaAthlete_Debug.json");
        var dto = json.FromJson<EspnFootballAthleteDto>()!;
        // The Debug fixture ships no `hand`, so add one — the update path must
        // refresh the null hand columns to these values.
        dto.Hand = new EspnAthleteHandDto { Type = "LEFT", Abbreviation = "L", DisplayValue = "Left" };
        var mutatedJson = dto.ToJson();

        var athleteIdentity = generator.Generate(dto.Ref);

        // Seed an existing athlete (null hand) with a matching external id so the
        // processor finds it and takes the update path.
        var existing = new FootballAthlete
        {
            Id = athleteIdentity.CanonicalId,
            FirstName = "Stale",
            LastName = "Stale",
            DisplayName = "Stale",
            ShortName = "Stale",
            CreatedUtc = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = Guid.NewGuid()
        };
        existing.ExternalIds.Add(new AthleteExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            Value = athleteIdentity.UrlHash,
            SourceUrl = athleteIdentity.CleanUrl,
            SourceUrlHash = athleteIdentity.UrlHash
        });
        await FootballDataContext.Athletes.AddAsync(existing);
        await FootballDataContext.SaveChangesAsync();
        FootballDataContext.ChangeTracker.Clear();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.SeasonYear, 2025)
            .With(x => x.DocumentType, DocumentType.Athlete)
            .With(x => x.UrlHash, athleteIdentity.UrlHash)
            .With(x => x.Document, mutatedJson)
            .Create();

        var sut = Mocker.CreateInstance<FootballAthleteDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert — hand columns refreshed from null to the fixture's values.
        var updated = await FootballDataContext.Athletes
            .OfType<FootballAthlete>()
            .AsNoTracking()
            .FirstAsync(x => x.Id == athleteIdentity.CanonicalId);

        updated.HandType.Should().Be("LEFT");
        updated.HandAbbreviation.Should().Be("L");
        updated.HandDisplayValue.Should().Be("Left");
    }
}
