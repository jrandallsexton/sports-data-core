using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class AthleteDocumentProcessorTests :
    ProducerTestBase<AthleteDocumentProcessor>
{
    private const string SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/athletes/4567747";
    private readonly string _urlHash = SourceUrl.UrlHash();

    [Fact]
    public async Task WhenAthleteIsValid_ShouldCreateAthlete()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var publishEndpoint = Mocker.GetMock<IBus>();
        var sut = Mocker.CreateInstance<AthleteDocumentProcessor>();

        var json = await LoadJsonTestData("EspnFootballNcaaAthlete_Active.json");
        var dto = json.FromJson<EspnFootballAthleteDto>();

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
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.Athlete)
            .With(x => x.UrlHash, _urlHash)
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
}
