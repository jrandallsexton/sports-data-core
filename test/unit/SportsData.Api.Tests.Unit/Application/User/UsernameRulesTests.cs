using FluentAssertions;

using SportsData.Api.Application.User;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.User;

public class UsernameRulesTests
{
    [Theory]
    [InlineData("JRandall", "jrandall")]
    [InlineData("  Foo_Bar ", "foo_bar")]
    [InlineData("a.b.c", "abc")]
    [InlineData("hi there!", "hithere")]
    [InlineData(null, "")]
    public void Normalize_LowercasesAndStripsIllegalChars(string? raw, string expected)
    {
        UsernameNormalizer.Normalize(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData("jrandallsexton")]
    [InlineData("JRandall")]          // caps allowed (lowercased on store)
    [InlineData("a_b_c")]
    [InlineData("user99")]
    public void IsValid_AcceptsCleanHandles(string raw)
    {
        UsernameNormalizer.IsValid(raw).Should().BeTrue();
    }

    [Theory]
    [InlineData("ab")]                // too short
    [InlineData("john smith")]        // space
    [InlineData("a.b")]               // punctuation
    [InlineData("admin")]             // reserved
    [InlineData("SportDeets")]        // reserved (case-insensitive)
    [InlineData("this_handle_is_way_too_long_to_pass")] // > 30
    [InlineData("")]
    public void IsValid_RejectsBadHandles(string raw)
    {
        UsernameNormalizer.IsValid(raw).Should().BeFalse();
    }

    [Theory]
    [InlineData("jrandallsexton@gmail.com", null, "jrandallsexton")]
    [InlineData("foo+spam@bar.com", null, "foo")]               // strips +tag
    [InlineData("a.b.c@x.com", null, "abc")]                    // strips dots
    [InlineData("x@y.com", "Snarky Name", "snarkyname")]        // local-part too short -> displayName
    [InlineData("x@y.com", "!!", "user")]                       // both too short -> last resort
    public void BuildSeed_PrefersEmailLocalPart_ThenDisplayName_ThenUser(
        string email, string? displayName, string expected)
    {
        UsernameGenerator.BuildSeed(email, displayName).Should().Be(expected);
    }

    [Fact]
    public void BuildSeed_TruncatesToMaxLength()
    {
        var seed = UsernameGenerator.BuildSeed("abcdefghijklmnopqrstuvwxyzabcdefghij@x.com", null);
        seed.Length.Should().Be(UsernameNormalizer.MaxLength);
    }

    [Fact]
    public void WithSuffix_TrimsSeedSoCombinedFitsMaxLength()
    {
        var seed = new string('a', UsernameNormalizer.MaxLength);
        var result = UsernameGenerator.WithSuffix(seed, 12);
        result.Length.Should().Be(UsernameNormalizer.MaxLength);
        result.Should().EndWith("12");
    }
}
