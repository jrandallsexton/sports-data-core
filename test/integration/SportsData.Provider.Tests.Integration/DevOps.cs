using FluentAssertions;

using Xunit;

namespace SportsData.Provider.Tests.Integration
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
