using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    public class EventCompetitionDocumentProcessorTests :
        ProducerTestBase<EventCompetitionDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task CanDeserializeCompetitionBroadcasts()
        {
            // arrange
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionBroadcasts.json");

            // act
            var broadcastsDto = documentJson.FromJson<EspnEventCompetitionBroadcastDto>();

            // assert
            broadcastsDto.Should().NotBeNull();
            broadcastsDto.Items.Should().NotBeNull();
            broadcastsDto.Items.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public async Task WhenEntityDoesNotExist_IsAdded()
        {
            // arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();

            // Create the parent contest entity before processing the competition document
            var contest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Sport, Sport.FootballNcaa)
                .Create();

            await base.FootballDataContext.Contests.AddAsync(contest);
            await base.FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, contest.Id.ToString)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334?lang=en".UrlHash())
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            contest = await base.FootballDataContext.Contests
                .FirstOrDefaultAsync(c => c.Id == contest.Id);

            contest.Should().NotBeNull();
        }
    }
}
