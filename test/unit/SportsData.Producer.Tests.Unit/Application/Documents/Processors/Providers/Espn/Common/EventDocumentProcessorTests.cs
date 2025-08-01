using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
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
        var bus = Mocker.GetMock<IBus>();
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

        // TODO: Load FranchiseSeason for both competitors

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
        var bus = Mocker.GetMock<IBus>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json");

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
        var bus = Mocker.GetMock<IBus>();
        var sut = Mocker.CreateInstance<EventDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEvent.json");
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