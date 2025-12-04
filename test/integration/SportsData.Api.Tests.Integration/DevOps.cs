using FluentAssertions;

using Xunit;

namespace SportsData.Api.Tests.Integration
{
    public class DevOps
    {
        [Fact]
        public void DevopsTest()
        {
            true.Should().BeTrue();
        }
    }
}
