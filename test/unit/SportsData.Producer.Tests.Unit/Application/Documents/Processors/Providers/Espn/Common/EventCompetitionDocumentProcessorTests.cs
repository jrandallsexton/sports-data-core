using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
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
            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var bus = Mocker.GetMock<IPublishEndpoint>();

            var dto = documentJson.FromJson<EspnEventCompetitionDto>();

            Guid homeId = Guid.Empty;
            Guid awayId = Guid.Empty;

            foreach (var competitor in dto.Competitors)
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

            // Create the parent contest entity before processing the competition document
            var contest = Fixture.Build<Contest>()
                .OmitAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Name, "Test")
                .With(x => x.ShortName, "Test")
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Competitions, new List<Competition>())
                .With(x => x.HomeTeamFranchiseSeasonId, homeId)
                .With(x => x.AwayTeamFranchiseSeasonId, awayId)
                .Create();

            await base.FootballDataContext.Contests.AddAsync(contest);
            await base.FootballDataContext.SaveChangesAsync();

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

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

            bus.Verify(x => x.Publish(It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionStatus), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
