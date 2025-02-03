using FluentAssertions;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Golf;

using Xunit;

namespace SportsData.Core.Tests.Unit.Infrastructure.DataSources.Espn.Dtos
{
    public class EspnAthleteDtoTests
    {
        [Fact]
        public async Task WhenValidJson_DtoShouldDeserialize()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnAthleteDtoFootballNcaaActive.json");
            
            // act
            var dto = json.FromJson<EspnFootballAthleteDto>();

            // assert
            dto.Status.Name.Should().Be("Active");
            dto.Links.Count.Should().Be(7);
            dto.BirthPlace.City.Should().Be("Saint Rose");
            dto.BirthPlace.State.Should().Be("LA");
            dto.BirthPlace.Country.Should().Be("USA");
            dto.Position.Name.Should().Be("Running Back");
            dto.Position.Id.Should().Be(9);
        }

        [Fact]
        public async Task WhenValidGolfJson_DtoShouldDeserialize()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Espn/EspnAthleteDtoGolfPga.json");

            // act
            var dto = json.FromJson<EspnGolfAthleteDto>();

            // assert
            dto.Status.Name.Should().Be("Active");
            dto.Links.Count.Should().Be(9);
            dto.BirthPlace.City.TrimEnd().Should().Be("Dallas");
            dto.BirthPlace.State.TrimEnd().Should().Be("Texas");
            dto.BirthPlace.Country.TrimEnd().Should().Be("United States");
            dto.DebutYear.Should().Be(2014);
            dto.TurnedPro.Should().Be(2018);
            dto.IsAmateur.Should().BeFalse();
            dto.Hand.DisplayValue.Should().Be("Right");
        }
    }
}
