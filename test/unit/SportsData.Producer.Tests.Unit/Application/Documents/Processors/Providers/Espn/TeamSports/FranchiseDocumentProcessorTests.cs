//using AutoFixture;

//using FluentAssertions;

//using MassTransit;

//using Microsoft.EntityFrameworkCore;

//using Moq;

//using SportsData.Core.Common;
//using SportsData.Core.Eventing.Events.Franchise;
//using SportsData.Producer.Application.Documents.Processors.Commands;
//using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
//using SportsData.Producer.Infrastructure.Data.Entities;

//using Xunit;

//namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports
//{
//    public class FranchiseDocumentProcessorTests : UnitTestBase<FranchiseDocumentProcessor>
//    {
//        [Fact]
//        public async Task WhenEntityDoesNotExist_VenueDoesExist_IsAdded()
//        {
//            // arrange
//            var bus = Mocker.GetMock<IPublishEndpoint>();

//            var sut = Mocker.CreateInstance<FranchiseDocumentProcessor>();

//            var documentJson = await LoadJsonTestData("EspnFootballNcaaFranchise.json");

//            var command = Fixture.Build<ProcessDocumentCommand>()
//                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
//                .With(x => x.Sport, Sport.FootballNcaa)
//                .With(x => x.DocumentType, DocumentType.Franchise)
//                .With(x => x.Document, documentJson)
//                .OmitAutoProperties()
//                .Create();

//            // add venue to test db
//            var venueId = Guid.NewGuid();
//            await base.FootballDataContext.Venues
//                .AddAsync(new Venue()
//                {
//                    Id = venueId,
//                    Name = "Tiger Stadium (LA)",
//                    ShortName = "Tiger Stadium",
//                    ExternalIds =
//                    [
//                        new VenueExternalId()
//                        {
//                            Id = Guid.NewGuid(),
//                            Provider = SourceDataProvider.Espn,
//                            Value = "3958"
//                        }
//                    ]
//                });
//            await base.FootballDataContext.SaveChangesAsync();

//            // act
//            await sut.ProcessAsync(command);

//            // assert
//            var newEntity = await base.TeamSportDataContext.Franchises
//                .AsNoTracking()
//                .FirstOrDefaultAsync();

//            newEntity.Should().NotBeNull();
//            newEntity.VenueId.Should().Be(venueId);

//            bus.Verify(x => x.Publish(It.IsAny<FranchiseCreated>(), It.IsAny<CancellationToken>()), Times.Once);
//        }

//        [Fact]
//        public async Task WhenEntityDoesNotExist_VenueDoesNotExist_IsAdded()
//        {
//            // arrange
//            var bus = Mocker.GetMock<IPublishEndpoint>();

//            var sut = Mocker.CreateInstance<FranchiseDocumentProcessor>();

//            var documentJson = await LoadJsonTestData("EspnFootballNcaaFranchise.json");

//            var command = Fixture.Build<ProcessDocumentCommand>()
//                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
//                .With(x => x.Sport, Sport.FootballNcaa)
//                .With(x => x.DocumentType, DocumentType.Franchise)
//                .With(x => x.Document, documentJson)
//                .OmitAutoProperties()
//                .Create();

//            // act
//            await sut.ProcessAsync(command);

//            // assert
//            var newEntity = await base.TeamSportDataContext.Franchises
//                .AsNoTracking()
//                .FirstOrDefaultAsync();

//            newEntity.Should().NotBeNull();
//            newEntity.VenueId.Should().Be(Guid.Empty);

//            bus.Verify(x => x.Publish(It.IsAny<FranchiseCreated>(), It.IsAny<CancellationToken>()), Times.Once);
//        }

//        [Fact]
//        public async Task WhenEntityExists_IsUpdated()
//        {
//            // arrange
//            var bus = Mocker.GetMock<IPublishEndpoint>();

//            var sut = Mocker.CreateInstance<FranchiseDocumentProcessor>();

//            var documentJson = await LoadJsonTestData("EspnFootballNcaaFranchise.json");

//            var command = Fixture.Build<ProcessDocumentCommand>()
//                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
//                .With(x => x.Sport, Sport.FootballNcaa)
//                .With(x => x.DocumentType, DocumentType.Franchise)
//                .With(x => x.Document, documentJson)
//                .OmitAutoProperties()
//                .Create();

//            // add venue to test db
//            var venueId = Guid.NewGuid();
//            await base.FootballDataContext.Venues
//                .AddAsync(new Venue()
//                {
//                    Id = venueId,
//                    Name = "Tiger Stadium (LA)",
//                    ShortName = "Tiger Stadium",
//                    ExternalIds =
//                    [
//                        new VenueExternalId()
//                        {
//                            Id = Guid.NewGuid(),
//                            Provider = SourceDataProvider.Espn,
//                            Value = "3958"
//                        }
//                    ]
//                });
//            await base.FootballDataContext.SaveChangesAsync();

//            var franchise = Fixture.Build<Franchise>()
//                .WithAutoProperties()
//                .With(x => x.VenueId, Guid.Empty)
//                .With(x => x.ExternalIds, [
//                    new FranchiseExternalId()
//                    {
//                        Id = Guid.NewGuid(),
//                        Provider = SourceDataProvider.Espn,
//                        Value = "99"
//                    }
//                ])
//                .Create();

//            await base.FootballDataContext.Franchises
//                .AddAsync(franchise);
//            await base.FootballDataContext.SaveChangesAsync();

//            // act
//            await sut.ProcessAsync(command);

//            // assert
//            var updatedEntity = await base.TeamSportDataContext.Franchises
//                .AsNoTracking()
//                .FirstOrDefaultAsync();

//            updatedEntity.Should().NotBeNull();
//            updatedEntity.VenueId.Should().Be(venueId);

//            bus.Verify(x => x.Publish(It.IsAny<FranchiseCreated>(), It.IsAny<CancellationToken>()), Times.Never);
//        }
//    }
//}
