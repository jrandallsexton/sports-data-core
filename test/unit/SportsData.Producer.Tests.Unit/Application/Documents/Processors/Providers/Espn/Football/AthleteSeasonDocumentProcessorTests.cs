using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class AthleteSeasonDocumentProcessorTests :
    ProducerTestBase<AthleteSeasonDocumentProcessor>
{
    private const string SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/4426333";
    private readonly string _urlHash = SourceUrl.UrlHash();

    [Fact]
    public async Task WhenAthleteSeasonIsValid_ShouldCreateAthleteSeason()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<AthleteSeasonDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthleteSeason.json");
        var dto = json.FromJson<EspnAthleteSeasonDto>();

        var franchiseSeasonIdentity = generator.Generate(dto.Team.Ref!);
        var positionIdentity = generator.Generate(dto.Position.Ref!);

        var athleteRef = $"http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/{dto.Id}";
        var athleteIdentity = generator.Generate(athleteRef);

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Seasons, new List<FranchiseSeason>())
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.SeasonYear, 2024)
            .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
            {
            new()
            {
                Id = Guid.NewGuid(),
                Provider = SourceDataProvider.Espn,
                SourceUrl = franchiseSeasonIdentity.CleanUrl,
                SourceUrlHash = franchiseSeasonIdentity.UrlHash,
                Value = franchiseSeasonIdentity.UrlHash
            }
            })
            .Create();

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

        var athleteId = Guid.NewGuid();
        var athlete = Fixture.Build<FootballAthlete>()
            .WithAutoProperties()
            .With(x => x.Id, athleteId)
            .With(x => x.LastName, dto.LastName)
            .With(x => x.FirstName, dto.FirstName)
            .With(x => x.Seasons, new List<AthleteSeason>())
            .Create();

        var athleteExternalId = new AthleteExternalId
        {
            Id = Guid.NewGuid(),
            AthleteId = athleteId,
            Provider = SourceDataProvider.Espn,
            SourceUrl = athleteRef,
            SourceUrlHash = athleteIdentity.UrlHash,
            Value = dto.Id
        };

        await FootballDataContext.AthletePositions.AddAsync(position);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.Athletes.AddAsync(athlete);
        await FootballDataContext.AthleteExternalIds.AddAsync(athleteExternalId);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.AthleteSeason)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, _urlHash)
            .Without(x => x.ParentId) // no longer needed
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var entity = await FootballDataContext.AthleteSeasons.FirstOrDefaultAsync();
        entity.Should().NotBeNull();
        entity!.AthleteId.Should().Be(athlete.Id);
        entity.PositionId.Should().Be(position.Id);
        entity.FranchiseSeasonId.Should().Be(franchiseSeason.Id);

        bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
    }

}
