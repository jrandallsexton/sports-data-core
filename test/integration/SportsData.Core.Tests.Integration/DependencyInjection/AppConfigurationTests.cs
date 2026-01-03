using FluentAssertions;

using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Core.Tests.Integration.Config;

public class AppConfigurationTests : IntegrationTestBase<AppConfigurationTests>
{
    public AppConfigurationTests() : base(label: "Dev") { }

    [Fact]
    public void LoggingConfiguration_IsCorrectlyLoaded()
    {
        CommonConfig.Logging.Should().NotBeNull();
        CommonConfig.Logging.MinimumLevel.Should().Be("Warning");
        CommonConfig.Logging.SeqMinimumLevel.Should().Be("Information");  // Dev environment uses Information level

        CommonConfig.Logging.Overrides.Should().ContainKey("Microsoft");
        CommonConfig.Logging.Overrides["Microsoft"].Should().Be("Error");

        CommonConfig.Logging.Overrides.Should().ContainKey("System");
        CommonConfig.Logging.Overrides["System"].Should().Be("Error");

        CommonConfig.Logging.Overrides.Should().ContainKey("SportsData");
        CommonConfig.Logging.Overrides["SportsData"].Should().Be("Warning");
    }
}