using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class TeamSeasonDocumentProcessorTests :
    ProducerTestBase<TeamSeasonDocumentProcessor<FootballDataContext>>
{
    private const string SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/99?lang=en";
    private readonly string _urlHash = SourceUrl.UrlHash();

    [Fact]
    public async Task WhenFranchiseSeasonDoesNotExist_ShouldCreateFranchiseSeasonAndPublishCreatedEvent()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaTeamSeason.json");

        var franchiseIdentity = generator.Generate(SourceUrl);

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Franchise")
            .With(x => x.ExternalIds, new List<FranchiseExternalId>
            {
                Fixture.Build<FranchiseExternalId>()
                    .WithAutoProperties()
                    .With(x => x.Provider, SourceDataProvider.Espn)
                    .With(x => x.SourceUrl, franchiseIdentity.CleanUrl)
                    .With(x => x.SourceUrlHash, franchiseIdentity.UrlHash)
                    .With(x => x.Value, franchiseIdentity.UrlHash)
                    .Create()
            })
            .With(x => x.Seasons, new List<FranchiseSeason>())
            .Create();

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.TeamSeason)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, _urlHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var fs = await FootballDataContext.FranchiseSeasons.FirstOrDefaultAsync();
        fs.Should().NotBeNull();
        fs!.FranchiseId.Should().Be(franchise.Id);
        fs.SeasonYear.Should().Be(2024);

        bus.Verify(x => x.Publish(It.IsAny<FranchiseSeasonCreated>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenFranchiseSeasonAlreadyExists_ShouldSkipCreationAndNotPublishCreatedEvent()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<TeamSeasonDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaTeamSeason.json");

        var franchiseId = Guid.NewGuid();
        var season = 2024;
        var franchiseSeasonId = DeterministicGuid.Combine(franchiseId, season);

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseSeasonId)
            .With(x => x.FranchiseId, franchiseId)
            .With(x => x.SeasonYear, season)
            .Create();

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.Id, franchiseId)
            .With(x => x.Name, "Test Franchise")
            .With(x => x.ExternalIds, new List<FranchiseExternalId>
            {
                Fixture.Build<FranchiseExternalId>()
                    .With(x => x.Provider, SourceDataProvider.Espn)
                    .With(x => x.SourceUrl, SourceUrl)
                    .With(x => x.SourceUrlHash, _urlHash)
                    .With(x => x.Value, _urlHash)
                    .Create()
            })
            .With(x => x.Seasons, new List<FranchiseSeason> { franchiseSeason })
            .Create();

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, season)
            .With(x => x.DocumentType, DocumentType.TeamSeason)
            .With(x => x.Document, json)
            .With(x => x.UrlHash, _urlHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var count = await FootballDataContext.FranchiseSeasons.CountAsync();
        count.Should().Be(1);

        bus.Verify(x => x.Publish(It.IsAny<FranchiseSeasonCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
