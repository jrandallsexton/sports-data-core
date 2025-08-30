using FluentAssertions;

using Xunit;

namespace SportsData.Producer.Tests.Unit;

public class DevOpsTests
{
    [Fact]
    public void TrueShouldBeTrue()
    {
        true.Should().BeTrue();
    }
}