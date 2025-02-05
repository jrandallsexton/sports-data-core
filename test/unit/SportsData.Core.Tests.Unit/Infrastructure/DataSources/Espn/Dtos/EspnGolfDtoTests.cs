using FluentAssertions;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnGolfDtoTests
    {
        [Fact]
        public async Task WhenValidJson_DtoShouldDeserialize()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnGolfCalendar.json");

            // act
            var dto = json.FromJson<EspnGolfCalendarDto>();

            // assert
            dto.EventDate.EventDateType.Should().Be("ondays");
        }

        [Fact]
        public async Task WhenValidEventJson_DtoShouldDeserialize()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnGolfPgaEvent.json");

            // act
            var dto = json.FromJson<EspnGolfEventDto>();

            // assert
            dto.Name.Should().Be("The American Express");
            dto.TimeValid.Should().BeTrue();
            dto.Competitions.Count.Should().Be(1);
            dto.Competitions.First().ScoringSystem.Name.Should().Be("Medal");
            dto.Links.Count.Should().Be(2);
            dto.Venues.Count.Should().Be(3);
        }
    }
}
