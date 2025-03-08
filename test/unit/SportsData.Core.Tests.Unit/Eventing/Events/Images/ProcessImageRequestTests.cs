using FluentAssertions;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;

using Xunit;

namespace SportsData.Core.Tests.Unit.Eventing.Events.Images
{
    public class ProcessImageRequestTests
    {
        [Fact]
        public async Task SerializationTests()
        {
            // arrange
            var json = await File.ReadAllTextAsync($"../../../Data/Events/ProcessImageRequest.json");

            // act
            var dto = json.FromJson<ProcessImageRequest>();

            // assert
            dto.Should().NotBeNull();
        }
    }
}
