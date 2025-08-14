using AutoFixture;

using FluentAssertions;

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
    public class EventCompetitionBroadcastDocumentProcessorTests
    : ProducerTestBase<EventCompetitionBroadcastDocumentProcessor<FootballDataContext>>
    {

        [Fact]
        public async Task WhenJsonIsValid_DtoDeserializes()
        {
            // arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionBroadcasts.json");

            // act
            var dto = json.FromJson<EspnEventCompetitionBroadcastDto>();

            // assert
            dto.Should().NotBeNull();
        }

        [Fact]
        public async Task WhenDataIsValid_HappyPathWorks()
        {
            // arrange
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionBroadcasts.json");
            var dto = json.FromJson<EspnEventCompetitionBroadcastDto>();

            var competitionIdentity = generator.Generate(dto.Items.First().Competition.Ref);

            var competition = Fixture.Build<Competition>()
                .OmitAutoProperties()
                .With(x => x.Id, competitionIdentity.CanonicalId)
                .With(x => x.ExternalIds, new List<CompetitionExternalId>
                {
                    new()
                    {
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitionIdentity.CleanUrl,
                        SourceUrlHash = competitionIdentity.UrlHash,
                        Value = competitionIdentity.UrlHash,
                        Id = Guid.NewGuid()
                    }
                })
                .Create();
            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.DocumentType, DocumentType.Event)
                .With(x => x.Season, 2024)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.ParentId, competitionIdentity.CanonicalId.ToString())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionBroadcastDocumentProcessor<FootballDataContext>>();
            
            // act
            await sut.ProcessAsync(command);

            // assert
            var created = await FootballDataContext.Broadcasts.ToListAsync();
            created.Should().NotBeNull().And.HaveCount(dto.Items.Count);
        }
    }
}
