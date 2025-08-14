using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

public class EventDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    [Fact]
    public async Task WhenEntityDoesNotExist_VenueExists_ShouldAddWithVenue()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        Mocker.GetMock<IProvideProviders>()
            .Setup(s => s.GetExternalDocument(It.IsAny<GetExternalDocumentQuery>()))
            .ReturnsAsync(() => Fixture.Build<GetExternalDocumentResponse>()
                .OmitAutoProperties()
                .Create());

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json");

        var venueUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/6501";
        var venueHash = venueUrl.UrlHash();
        var venueId = Guid.NewGuid();

        await FootballDataContext.Venues.AddAsync(new Venue
        {
            Id = venueId,
            Name = "Tiger Stadium",
            Slug = "tiger-stadium",
            City = "Baton Rouge",
            State = "LA",
            PostalCode = "71077",
            ExternalIds =
            [
                new VenueExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = venueHash,
                    Value = venueHash,
                    SourceUrl = venueUrl
                }
            ]
        });

        await FootballDataContext.SaveChangesAsync();

        var dto = json.FromJson<EspnEventDto>();

        var seasonId = Guid.NewGuid();
        var seasonTypeIdentity = generator.Generate(dto.SeasonType.Ref);

        var season = Fixture.Build<Season>()
            .OmitAutoProperties()
            .With(x => x.Id, seasonId)
            .With(x => x.Name, "2024")
            .With(x => x.Phases, [])
            .Create();
        await FootballDataContext.Seasons.AddAsync(season);
        await FootballDataContext.SaveChangesAsync();

        var seasonPhase = Fixture.Build<SeasonPhase>()
            .OmitAutoProperties()
            .With(x => x.Id, seasonTypeIdentity.CanonicalId)
            .With(x => x.Name, "Regular Season")
            .With(x => x.Abbreviation, "reg")
            .With(x => x.Slug, "reg-season")
            .With(x => x.SeasonId, seasonId)
            .With(x => x.ExternalIds, new List<SeasonPhaseExternalId>()
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = seasonTypeIdentity.CleanUrl,
                    SourceUrlHash = seasonTypeIdentity.UrlHash,
                    Value = seasonTypeIdentity.UrlHash
                }
            })
            .Create();
        await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
        await FootballDataContext.SaveChangesAsync();

        var seasonWeekIdentity = generator.Generate(dto.Week.Ref);
        var seasonWeek = Fixture.Build<SeasonWeek>()
            .OmitAutoProperties()
            .With(x => x.Id, seasonWeekIdentity.CanonicalId)
            .With(x => x.SeasonPhaseId, seasonPhase.Id)
            .With(x => x.ExternalIds, new List<SeasonWeekExternalId>()
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = seasonWeekIdentity.CleanUrl,
                    SourceUrlHash = seasonWeekIdentity.UrlHash,
                    Value = seasonWeekIdentity.UrlHash
                }
            })
            .Create();
        await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);
        await FootballDataContext.SaveChangesAsync();

        Guid homeId = Guid.Empty;
        Guid awayId = Guid.Empty;

        foreach (var competitor in dto.Competitions.First().Competitors)
        {
            var identity = generator.Generate(competitor.Team.Ref);

            if (competitor.HomeAway == "home")
            {
                homeId = identity.CanonicalId;
            }
            else
            {
                awayId = identity.CanonicalId;
            }

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Abbreviation, "Test")
                .With(x => x.DisplayName, "Test Franchise Season")
                .With(x => x.DisplayNameShort, "Test FS")
                .With(x => x.Slug, identity.CanonicalId.ToString())
                .With(x => x.Location, "Test Location")
                .With(x => x.Name, "Test Franchise Season")
                .With(x => x.ColorCodeHex, "#FFFFFF")
                .With(x => x.ColorCodeAltHex, "#000000")
                .With(x => x.IsActive, true)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
                {
                        new()
                        {
                            Id = Guid.NewGuid(),
                            Provider = SourceDataProvider.Espn,
                            SourceUrl = identity.CleanUrl,
                            SourceUrlHash = identity.UrlHash,
                            Value = identity.UrlHash
                        }
                })
                .Create();

            await base.FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }
        await base.FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Event)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var created = await FootballDataContext.Contests.FirstOrDefaultAsync();
        created.Should().NotBeNull();
        created!.VenueId.Should().Be(venueId);

        bus.Verify(x => x.Publish(It.IsAny<ContestCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetition), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenEntityDoesNotExist_VenueMissing_ShouldPublishDocumentRequested()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        Mocker.GetMock<IProvideProviders>()
            .Setup(s => s.GetExternalDocument(It.IsAny<GetExternalDocumentQuery>()))
            .ReturnsAsync(() => Fixture.Build<GetExternalDocumentResponse>()
                .OmitAutoProperties()
                .Create());

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json"); var dto = json.FromJson<EspnEventDto>();
        Guid homeId = Guid.Empty;
        Guid awayId = Guid.Empty;

        foreach (var competitor in dto.Competitions.First().Competitors)
        {
            var identity = generator.Generate(competitor.Team.Ref);

            if (competitor.HomeAway == "home")
            {
                homeId = identity.CanonicalId;
            }
            else
            {
                awayId = identity.CanonicalId;
            }

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Abbreviation, "Test")
                .With(x => x.DisplayName, "Test Franchise Season")
                .With(x => x.DisplayNameShort, "Test FS")
                .With(x => x.Slug, identity.CanonicalId.ToString())
                .With(x => x.Location, "Test Location")
                .With(x => x.Name, "Test Franchise Season")
                .With(x => x.ColorCodeHex, "#FFFFFF")
                .With(x => x.ColorCodeAltHex, "#000000")
                .With(x => x.IsActive, true)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash,
                        Value = identity.UrlHash
                    }
                })
                .Create();

            await base.FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }
        await base.FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Event)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var created = await FootballDataContext.Contests.FirstOrDefaultAsync();
        created.Should().NotBeNull();
        created!.VenueId.Should().BeNull();

        bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d =>
            d.DocumentType == DocumentType.Venue &&
            d.ParentId == string.Empty), It.IsAny<CancellationToken>()), Times.Once);

        bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d =>
            d.DocumentType == DocumentType.EventCompetition), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenEntityAlreadyExists_ShouldSkipCreation_AndNotPublishContestCreated()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IPublishEndpoint>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json");

        var dto = json.FromJson<EspnEventDto>();
        Guid homeId = Guid.Empty;
        Guid awayId = Guid.Empty;

        foreach (var competitor in dto.Competitions.First().Competitors)
        {
            var identity = generator.Generate(competitor.Team.Ref);

            if (competitor.HomeAway == "home")
            {
                homeId = identity.CanonicalId;
            }
            else
            {
                awayId = identity.CanonicalId;
            }

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Abbreviation, "Test")
                .With(x => x.DisplayName, "Test Franchise Season")
                .With(x => x.DisplayNameShort, "Test FS")
                .With(x => x.Slug, identity.CanonicalId.ToString())
                .With(x => x.Location, "Test Location")
                .With(x => x.Name, "Test Franchise Season")
                .With(x => x.ColorCodeHex, "#FFFFFF")
                .With(x => x.ColorCodeAltHex, "#000000")
                .With(x => x.IsActive, true)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.FranchiseId, Guid.NewGuid())
                .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash,
                        Value = identity.UrlHash
                    }
                })
                .Create();

            await base.FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }
        await base.FootballDataContext.SaveChangesAsync();

        var externalId = "401583027";

        var eventUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334?lang=en";

        var contest = Fixture.Build<Contest>()
            .With(x => x.ExternalIds, [
                new ContestExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = externalId,
                    SourceUrlHash = eventUrl.UrlHash(),
                    SourceUrl = eventUrl
                }
            ])
            .Create();

        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Event)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.UrlHash, contest.ExternalIds.First().SourceUrlHash)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        bus.Verify(x => x.Publish(It.IsAny<ContestCreated>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}