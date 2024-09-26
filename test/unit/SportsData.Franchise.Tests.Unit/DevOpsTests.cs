using FluentAssertions;
using Xunit;

namespace SportsData.Franchise.Tests.Unit
{
    public class DevOpsTests
    {
        [Fact]
        public void TrueShouldBeTrue()
        {
            true.Should().BeTrue();
        }
    }
}