using FluentAssertions;
using Xunit;

namespace SportsData.Provider.Tests.Unit
{
    public class DevOpsTests
    {
        [Fact]
        public void TrueShouldBeTrue()
        {
            true.Should().BeFalse();
        }
    }
}
