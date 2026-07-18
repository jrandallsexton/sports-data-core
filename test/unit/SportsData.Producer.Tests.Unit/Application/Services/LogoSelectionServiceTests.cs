#nullable enable
using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Services;

/// <summary>
/// Pins the FAIL-CLOSED contract of the logo selector: it returns ONLY a
/// sportDeets-generated mark, or null — never an ESPN / untagged (licensed)
/// logo. Also guards the fail-open shadowing regression (a season ESPN logo
/// hiding a franchise mark). See docs/logo-license-audit.md.
/// </summary>
public class LogoSelectionServiceTests
{
    private readonly LogoSelectionService _sut = new();

    private sealed class TestLogo : ILogo
    {
        public string OriginalUrlHash { get; set; } = string.Empty;
        public Uri Uri { get; set; } = null!;
        public long? Height { get; set; }
        public long? Width { get; set; }
        public List<string>? Rel { get; set; }
        public bool? IsForDarkBg { get; set; }
    }

    private static TestLogo Mark(string uri, params string[] rel) =>
        new() { Uri = new Uri(uri), Rel = rel.ToList() };

    // An ESPN-sourced / untagged logo — a real Rel that does NOT contain
    // "sportdeets-mark" (or none at all).
    private static TestLogo Espn(string uri, params string[] rel) =>
        new() { Uri = new Uri(uri), Rel = rel.Length == 0 ? null : rel.ToList() };

    [Fact]
    public void SelectLogoForDarkBackground_ReturnsMark_WhenMarkPresent()
    {
        var logos = new[]
        {
            Espn("https://espn/logo.png", "default"),
            Mark("https://sd/roundel.png", "sportdeets-mark", "roundel"),
        };

        _sut.SelectLogoForDarkBackground(logos)!.OriginalString
            .Should().Be("https://sd/roundel.png");
    }

    [Fact]
    public void SelectLogoForDarkBackground_ReturnsNull_WhenOnlyEspnLogos()
    {
        // The core guarantee: a licensed logo is never returned.
        var logos = new[]
        {
            Espn("https://espn/logo.png", "default"),
            Espn("https://espn/dark.png", "dark"),
            Espn("https://espn/plain.png"),
        };

        _sut.SelectLogoForDarkBackground(logos).Should().BeNull();
    }

    [Fact]
    public void SelectLogoForLightBackground_MatchesDark_MarksAreThemeAgnostic()
    {
        var logos = new[] { Mark("https://sd/roundel.png", "sportdeets-mark", "roundel") };

        _sut.SelectLogoForLightBackground(logos)!.OriginalString
            .Should().Be(_sut.SelectLogoForDarkBackground(logos)!.OriginalString);
    }

    [Fact]
    public void Select_PrefersRequestedDirection()
    {
        var logos = new[]
        {
            Mark("https://sd/hex.png", "sportdeets-mark", "hex"),
            Mark("https://sd/roundel.png", "sportdeets-mark", "roundel"),
        };

        _sut.SelectLogoForDarkBackground(logos, MarkDirection.Hex)!.OriginalString
            .Should().Be("https://sd/hex.png");
        _sut.SelectLogoForDarkBackground(logos, MarkDirection.Roundel)!.OriginalString
            .Should().Be("https://sd/roundel.png");
    }

    [Fact]
    public void Select_FallsBackToAnyMark_WhenRequestedDirectionMissing()
    {
        var logos = new[] { Mark("https://sd/hex.png", "sportdeets-mark", "hex") };

        _sut.SelectLogoForDarkBackground(logos, MarkDirection.Roundel)!.OriginalString
            .Should().Be("https://sd/hex.png");
    }

    [Fact]
    public void SelectWithFallback_PrefersSeasonMark_OverFranchiseMark()
    {
        var season = new[] { Mark("https://sd/season.png", "sportdeets-mark", "roundel") };
        var franchise = new[] { Mark("https://sd/franchise.png", "sportdeets-mark", "roundel") };

        _sut.SelectWithFallback(season, franchise)!.OriginalString
            .Should().Be("https://sd/season.png");
    }

    [Fact]
    public void SelectWithFallback_UsesFranchiseMark_WhenSeasonHasOnlyEspn()
    {
        // Regression guard for the fail-open shadowing bug: a season that has
        // only an ESPN logo (no mark) must NOT shadow the franchise mark.
        var season = new[] { Espn("https://espn/season.png", "default") };
        var franchise = new[] { Mark("https://sd/franchise.png", "sportdeets-mark", "roundel") };

        _sut.SelectWithFallback(season, franchise)!.OriginalString
            .Should().Be("https://sd/franchise.png");
    }

    [Fact]
    public void SelectWithFallback_ReturnsNull_WhenNoMarksAnywhere()
    {
        var season = new[] { Espn("https://espn/season.png", "default") };
        var franchise = new[] { Espn("https://espn/franchise.png", "default") };

        _sut.SelectWithFallback(season, franchise).Should().BeNull();
    }

    [Fact]
    public void SelectWithFallback_ReturnsNull_WhenBothEmptyOrNull()
    {
        _sut.SelectWithFallback(null, null).Should().BeNull();
        _sut.SelectWithFallback(Array.Empty<ILogo>(), Array.Empty<ILogo>()).Should().BeNull();
    }
}
