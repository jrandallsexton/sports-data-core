using FluentAssertions;

using Microsoft.Extensions.Configuration;

using SportsData.Api.Config;

using Xunit;

namespace SportsData.Api.Tests.Unit.Config;

/// <summary>
/// The AppConfig value is ONE key holding a nested JSON document. Azure only
/// expands that into hierarchical keys when the setting carries content-type
/// application/json, which the provisioning script drops on its import path —
/// so the section has a value but no children and the ordinary binder produced
/// an EMPTY dictionary. That failed silently: nothing reads a style until an
/// ATS matchup with a spread appears, and then every styled bot throws.
/// </summary>
public class SyntheticUserPickStylesConfigTests
{
    // The exact value stored in AppConfig (Local / Prod), verbatim.
    private const string ProductionValue =
        "{\"moderate\": {\"name\": \"moderate\", \"description\": \"Balanced risk tolerance\", \"thresholds\": [{\"maxSpread\": 7, \"minConfidence\": 0.5}, {\"maxSpread\": 10, \"minConfidence\": 0.6}, {\"maxSpread\": 14, \"minConfidence\": 0.7}, {\"maxSpread\": 21, \"minConfidence\": 0.8}, {\"maxSpread\": null, \"minConfidence\": 0.9}]}, \"conservative\": {\"name\": \"conservative\", \"description\": \"Risk-averse, higher confidence requirements\", \"thresholds\": [{\"maxSpread\": 7, \"minConfidence\": 0.6}, {\"maxSpread\": 10, \"minConfidence\": 0.7}, {\"maxSpread\": 14, \"minConfidence\": 0.8}, {\"maxSpread\": 21, \"minConfidence\": 0.9}, {\"maxSpread\": null, \"minConfidence\": 0.95}]}, \"aggressive\": {\"name\": \"aggressive\", \"description\": \"Risk-tolerant, trusts model predictions more\", \"thresholds\": [{\"maxSpread\": 7, \"minConfidence\": 0.5}, {\"maxSpread\": 10, \"minConfidence\": 0.55}, {\"maxSpread\": 14, \"minConfidence\": 0.6}, {\"maxSpread\": 21, \"minConfidence\": 0.7}, {\"maxSpread\": null, \"minConfidence\": 0.8}]}}";

    private const string Key = "SportsData.Api:SyntheticUserPickStyles";

    private static SyntheticUserPickStylesConfig Bind(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = value })
            .Build();

        var target = new SyntheticUserPickStylesConfig();
        SyntheticUserPickStylesConfig.BindFrom(configuration, Key)(target);
        return target;
    }

    [Fact]
    public void BindFrom_ParsesTheValueThatIsActuallyStoredInAppConfig()
    {
        var config = Bind(ProductionValue);

        config.Should().HaveCount(3);
        config.Keys.Should().BeEquivalentTo(["moderate", "conservative", "aggressive"]);

        var conservative = config["conservative"];
        conservative.Description.Should().Be("Risk-averse, higher confidence requirements");
        conservative.Thresholds.Should().HaveCount(5);
        conservative.Thresholds[0].MaxSpread.Should().Be(7);
        conservative.Thresholds[0].MinConfidence.Should().Be(0.6);

        // The catch-all tier: "maxSpread": null means "every spread above the
        // previous tier", and must survive as null rather than 0.
        conservative.Thresholds[4].MaxSpread.Should().BeNull();
        conservative.Thresholds[4].MinConfidence.Should().Be(0.95);
    }

    /// <summary>
    /// This is the regression that mattered. The ordinary section binder saw a
    /// value with no children and produced an empty dictionary, which is what
    /// "initialized with 0 pick styles" meant at every startup.
    /// </summary>
    [Fact]
    public void SectionBinder_OnTheSameValue_BindsEmpty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [Key] = ProductionValue })
            .Build();

        var viaSectionBinder = new SyntheticUserPickStylesConfig();
        configuration.GetSection(Key).Bind(viaSectionBinder);

        viaSectionBinder.Should().BeEmpty("a JSON blob has a value but no child keys");
    }

    [Fact]
    public void BindFrom_NormalisesStyleKeysToLowercase()
    {
        // GetPickStyle lowercases the lookup; a capitalised AppConfig key would
        // otherwise be a silent miss.
        var config = Bind("{\"Aggressive\": {\"name\": \"Aggressive\", \"description\": \"d\", \"thresholds\": []}}");

        config.Should().ContainKey("aggressive");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BindFrom_MissingValue_YieldsEmptyRatherThanThrowing(string? value)
    {
        // Startup must not hard-fail on a missing optional setting; the
        // provider logs the count so an empty load is visible.
        Bind(value).Should().BeEmpty();
    }
}
