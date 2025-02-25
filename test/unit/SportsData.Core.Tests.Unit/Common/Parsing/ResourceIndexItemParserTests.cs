using FluentAssertions;

using SportsData.Core.Common.Parsing;

using Xunit;

namespace SportsData.Core.Tests.Unit.Common.Parsing
{
    public class ResourceIndexItemParserTests
    {
        [Fact]
        public async Task ExtractEmbeddedLinks_WithEmbeddedLinks_Extracts()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnAthleteDtoFootballNfl.json");

            var sut = new ResourceIndexItemParser();

            // act
            var uris = sut.ExtractEmbeddedLinks(json);

            // assert
            uris.Should().NotBeNull().And.HaveCount(11);
        }
    }
}
