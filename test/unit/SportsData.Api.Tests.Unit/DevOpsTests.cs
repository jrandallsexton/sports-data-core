using FluentAssertions;
using Xunit;

namespace SportsData.Api.Tests.Unit
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
