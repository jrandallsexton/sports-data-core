using FluentAssertions;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnSeasonsDtoTests
    {
        [Fact]
        public async Task WhenValidJson_DtoShouldDeserialize()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnSeasonsDtoFootballNcaa.json");

            // act
            var dto = json.FromJson<EspnFootballSeasonDto>();

            // assert
            dto.Should().NotBeNull();
            dto.Types.Items.Count().Should().Be(dto.Types.Count);
        }
    }
}
