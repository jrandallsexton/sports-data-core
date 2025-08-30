using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports
{
    public class FranchiseDocumentProcessorTests : ProducerTestBase<FranchiseDocumentProcessor<TeamSportDataContext>>
    {
        [Fact]
        public async Task WhenEntityDoesNotExist_VenueDoesExist_IsAdded()
        {
            // arrange
            var bus = Mocker.GetMock<IEventBus>();
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var sut = Mocker.CreateInstance<FranchiseDocumentProcessor<TeamSportDataContext>>();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaFranchise.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.Franchise)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // add venue to test db
            var venueId = Guid.NewGuid();
            var venueUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/3958?lang=en&region=us";
            await base.FootballDataContext.Venues
                .AddAsync(new Venue()
                {
                    Id = venueId,
                    Name = "Tiger Stadium (LA)",
                    ShortName = "Tiger Stadium",
                    Slug = "tiger-stadium-la",
                    City = "Baton Rouge",
                    State = "LA",
                    PostalCode = "71077",
                    ExternalIds =
                    [
                        new VenueExternalId()
                        {
                            Id = Guid.NewGuid(),
                            Provider = SourceDataProvider.Espn,
                            Value = venueUrl.UrlHash(),
                            SourceUrlHash = venueUrl.UrlHash(),
                            SourceUrl = venueUrl
                        }
                    ]
                });
            await base.FootballDataContext.SaveChangesAsync();

            // act
            await sut.ProcessAsync(command);

            // assert
            var newEntity = await base.TeamSportDataContext.Franchises
                .AsNoTracking()
                .FirstOrDefaultAsync();

            newEntity.Should().NotBeNull();
            newEntity.VenueId.Should().Be(venueId);

            bus.Verify(x => x.Publish(It.IsAny<FranchiseCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhenEntityDoesNotExist_VenueDoesNotExist_IsAdded()
        {
            // arrange
            var bus = Mocker.GetMock<IEventBus>();
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var sut = Mocker.CreateInstance<FranchiseDocumentProcessor<TeamSportDataContext>>();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaFranchise.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.Franchise)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var newEntity = await base.TeamSportDataContext.Franchises
                .AsNoTracking()
                .FirstOrDefaultAsync();

            newEntity.Should().NotBeNull();
            newEntity.VenueId.Should().Be(Guid.Empty);

            bus.Verify(x => x.Publish(It.IsAny<FranchiseCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhenEntityExists_IsUpdated()
        {
            // arrange
            var bus = Mocker.GetMock<IEventBus>();

            var sut = Mocker.CreateInstance<FranchiseDocumentProcessor<TeamSportDataContext>>();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaFranchise.json");

            // add venue to test db
            var venueId = Guid.NewGuid();
            var venueUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/3958?lang=en&region=us";
            await base.FootballDataContext.Venues
                .AddAsync(new Venue()
                {
                    Id = venueId,
                    Name = "Tiger Stadium (LA)",
                    ShortName = "Tiger Stadium",
                    Slug = "tiger-stadium-la",
                    City = "Baton Rouge",
                    State = "LA",
                    PostalCode = "71077",
                    ExternalIds =
                    [
                        new VenueExternalId()
                        {
                            Id = Guid.NewGuid(),
                            Provider = SourceDataProvider.Espn,
                            Value = venueUrl.UrlHash(),
                            SourceUrlHash = venueUrl.UrlHash(),
                            SourceUrl = venueUrl
                        }
                    ]
                });

            var franchiseUrl =
                "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises/99?lang=en&region=us";
            var franchise = Fixture.Build<Franchise>()
                .WithAutoProperties()
                .With(x => x.VenueId, Guid.Empty)
                .With(x => x.ExternalIds, [
                    new FranchiseExternalId()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = franchiseUrl.UrlHash(),
                        SourceUrlHash = franchiseUrl.UrlHash(),
                        SourceUrl = franchiseUrl
                    }
                ])
                .Create();

            await base.FootballDataContext.Franchises.AddAsync(franchise);
            await base.FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.Franchise)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, franchise.ExternalIds.First().Value)
                .OmitAutoProperties()
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var updatedEntity = await base.TeamSportDataContext.Franchises
                .AsNoTracking()
                .FirstOrDefaultAsync();

            updatedEntity.Should().NotBeNull();
            updatedEntity.VenueId.Should().Be(venueId);

            bus.Verify(x => x.Publish(It.IsAny<FranchiseCreated>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
