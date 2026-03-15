using FluentAssertions;

using Moq;
using Moq.AutoMock;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Diagnostics.Metrics;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Infrastructure.Providers.Espn
{
    public class EspnApiClientTests
    {
        [Fact]
        public async Task GetResourceIndex_WhenValidData_ExtractsIdsForEachIndexItem()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/AthletesBySeasonResourceIndex.json");
            var dto = json.FromJson<EspnResourceIndexDto>();

            var mocker = new AutoMocker();
            mocker.GetMock<IMeterFactory>()
                .Setup(f => f.Create(It.IsAny<MeterOptions>()))
                .Returns(new Meter("test"));

            var sut = mocker.CreateInstance<EspnApiClient>();

            // act
            var result = sut.ExtractIds(dto, string.Empty);

            // assert
            result.Items.First().Id.Should().NotBe("0");
        }
    }
}
