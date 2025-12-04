using FluentAssertions;

using Xunit;

namespace SportsData.Producer.Tests.Integration
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
