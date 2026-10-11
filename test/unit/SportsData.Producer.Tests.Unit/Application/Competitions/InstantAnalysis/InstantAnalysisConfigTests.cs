#nullable enable
using FluentAssertions;

using Microsoft.Extensions.Configuration;

using Moq;

using SportsData.Producer.Application.Competitions.InstantAnalysis;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.InstantAnalysis;

public class InstantAnalysisConfigTests
{
    private static IConfiguration ConfigWith(string? enabled)
    {
        var config = new Mock<IConfiguration>();
        config.Setup(x => x[InstantAnalysisConfig.EnabledKey]).Returns(enabled);
        return config.Object;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("false")]
    [InlineData("yes")]
    public void WhenNotExplicitlyTrue_IsOff(string? value)
    {
        InstantAnalysisConfig.FromConfiguration(ConfigWith(value)).Enabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    public void WhenTrue_IsOn(string value)
    {
        InstantAnalysisConfig.FromConfiguration(ConfigWith(value)).Enabled.Should().BeTrue();
    }
}
