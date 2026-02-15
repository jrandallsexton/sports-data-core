using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Config;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Tests for EventDocumentProcessor.
/// Optimized to eliminate AutoFixture overhead.
/// </summary>
[Collection("Sequential")]
public class EventDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    // Helper method to setup common test data
    private async Task SetupSeasonDataAsync(ExternalRefIdentityGenerator generator, EspnEventDto dto)
    {
        var seasonId = Guid.NewGuid();
        var seasonTypeIdentity = generator.Generate(dto.SeasonType.Ref);

        // OPTIMIZATION: Direct instantiation
        var season = new Season
        {
            Id = seasonId,
            Name = "2024",
            Year = 2024,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Seasons.AddAsync(season);
        await FootballDataContext.SaveChangesAsync();

        // OPTIMIZATION: Direct instantiation
        var seasonPhase = new SeasonPhase
        {
            Id = seasonTypeIdentity.CanonicalId,
            Name = "Regular Season",
            Abbreviation = "reg",
            Slug = "reg-season",
            SeasonId = seasonId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<SeasonPhaseExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SeasonPhaseId = seasonTypeIdentity.CanonicalId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = seasonTypeIdentity.CleanUrl,
                    SourceUrlHash = seasonTypeIdentity.UrlHash,
                    Value = seasonTypeIdentity.UrlHash
                }
            }
        };
        await FootballDataContext.SeasonPhases.AddAsync(seasonPhase);
        await FootballDataContext.SaveChangesAsync();

        var seasonWeekIdentity = generator.Generate(dto.Week.Ref);
        
        // OPTIMIZATION: Direct instantiation
        var seasonWeek = new SeasonWeek
        {
            Id = seasonWeekIdentity.CanonicalId,
            SeasonPhaseId = seasonPhase.Id,
            Number = 1,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<SeasonWeekExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    SeasonWeekId = seasonWeekIdentity.CanonicalId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = seasonWeekIdentity.CleanUrl,
                    SourceUrlHash = seasonWeekIdentity.UrlHash,
                    Value = seasonWeekIdentity.UrlHash
                }
            }
        };
        await FootballDataContext.SeasonWeeks.AddAsync(seasonWeek);
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task SetupFranchiseSeasonsAsync(ExternalRefIdentityGenerator generator, EspnEventDto dto)
    {
        foreach (var competitor in dto.Competitions.First().Competitors)
        {
            var identity = generator.Generate(competitor.Team.Ref);

            // OPTIMIZATION: Direct instantiation
            var franchiseSeason = new FranchiseSeason
            {
                Id = Guid.NewGuid(),
                Abbreviation = "Test",
                DisplayName = "Test Franchise Season",
                DisplayNameShort = "Test FS",
                Slug = identity.CanonicalId.ToString(),
                Location = "Test Location",
                Name = "Test Franchise Season",
                ColorCodeHex = "#FFFFFF",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash,
                        Value = identity.UrlHash
                    }
                }
            };

            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }
        await FootballDataContext.SaveChangesAsync();
    }
    [Fact]
    public async Task WhenEntityDoesNotExist_VenueExists_ShouldAddWithVenue()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        Mocker.GetMock<IProvideProviders>()
            .Setup(s => s.GetExternalDocument(It.IsAny<GetExternalDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Fixture.Build<GetExternalDocumentResponse>()
                .OmitAutoProperties()
                .Create());

        var bus = Mocker.GetMock<IEventBus>();
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
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds =
            [
                new VenueExternalId
                {
                    Id = Guid.NewGuid(),
                    VenueId = venueId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = venueHash,
                    Value = venueHash,
                    SourceUrl = venueUrl
                }
            ]
        });

        await FootballDataContext.SaveChangesAsync();

        var dto = json.FromJson<EspnEventDto>();

        await SetupSeasonDataAsync(generator, dto!);
        await SetupFranchiseSeasonsAsync(generator, dto!);

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
        // TODO: This test reflects current bowl season processing behavior where missing venues
        // trigger a retry via ExternalDocumentNotSourcedException. Once we support per-dependency
        // flag overrides, update this test to verify Contest creation with VenueId=null when
        // EnableDependencyRequests=true for Venue dependencies.
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        Mocker.GetMock<IProvideProviders>()
            .Setup(s => s.GetExternalDocument(It.IsAny<GetExternalDocumentQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Fixture.Build<GetExternalDocumentResponse>()
                .OmitAutoProperties()
                .Create());

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json");

        await FootballDataContext.SaveChangesAsync();

        var dto = json.FromJson<EspnEventDto>();

        await SetupSeasonDataAsync(generator, dto!);
        await SetupFranchiseSeasonsAsync(generator, dto!);

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, json)
            .With(x => x.DocumentType, DocumentType.Event)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.AttemptCount, 0)
            .OmitAutoProperties()
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        // Current behavior: Missing venue triggers retry, Contest is not created
        var created = await FootballDataContext.Contests.FirstOrDefaultAsync();
        created.Should().BeNull("missing venue triggers retry, Contest creation is deferred");

        // Verify DocumentRequested published for missing Venue
        bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d =>
            d.DocumentType == DocumentType.Venue &&
            d.ParentId == string.Empty), It.IsAny<CancellationToken>()), Times.Once);

        // Verify retry scheduled via DocumentCreated with incremented AttemptCount
        bus.Verify(x => x.Publish(It.Is<DocumentCreated>(d =>
            d.AttemptCount == 1), It.IsAny<IDictionary<string, object>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task WhenEntityAlreadyExists_ShouldSkipCreation_AndNotPublishContestCreated()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json");

        var dto = json.FromJson<EspnEventDto>();

        await SetupFranchiseSeasonsAsync(generator, dto!);

        var externalId = "401583027";
        var eventUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334?lang=en";

        // OPTIMIZATION: Direct instantiation (was taking 26 seconds!)
        var contest = new Contest
        {
            Id = Guid.NewGuid(),
            Name = "Test Contest",
            ShortName = "Test",
            SeasonYear = 2024,
            Sport = Sport.FootballNcaa,
            StartDateUtc = DateTime.UtcNow,
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<ContestExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = externalId,
                    SourceUrlHash = eventUrl.UrlHash(),
                    SourceUrl = eventUrl
                }
            }
        };

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