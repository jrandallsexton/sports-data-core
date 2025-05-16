using FluentAssertions;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnLeagueDtoTests
    {
        [Fact]
        public async Task WhenValidJson_DtoShouldDeserialize()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnLeagueDtoFootballNcaa.json");

            // act
            var dto = json.FromJson<EspnLeagueDto>();

            // assert
            dto.Links.Count.Should().Be(11);
            dto.Name.Should().Be("NCAA - Football");
        }
    }
}
