using FluentAssertions;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    public class EventCompetitionCompetitorDocumentProcessorTests
        : ProducerTestBase<EventCompetitionCompetitorDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenJsonIsValid_DtoDeserializes()
        {
            // arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitor.json");

            // act
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            // assert
            dto.Should().NotBeNull();

            // factual assertions based on the test JSON

        }
    }
}
