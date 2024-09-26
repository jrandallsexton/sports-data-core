using FluentAssertions;
using Xunit;

namespace SportsData.Contest.Tests.Unit
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