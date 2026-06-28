using FluentAssertions;

using SportsData.Api.Application.UI.Devices;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Devices;

public class InstallationIdNormalizerTests
{
    [Fact]
    public void Normalize_LowercasesAndCanonicalizes_UppercaseGuid()
    {
        var canonical = Guid.NewGuid().ToString(); // lowercase "D" form

        InstallationIdNormalizer.Normalize(canonical.ToUpperInvariant())
            .Should().Be(canonical);
    }

    [Fact]
    public void Normalize_StripsBraces_FromGuid()
    {
        var guid = Guid.NewGuid();

        InstallationIdNormalizer.Normalize($"{{{guid}}}")
            .Should().Be(guid.ToString());
    }

    [Fact]
    public void Normalize_TrimsWhitespace_AroundGuid()
    {
        var canonical = Guid.NewGuid().ToString();

        InstallationIdNormalizer.Normalize($"  {canonical.ToUpperInvariant()}  ")
            .Should().Be(canonical);
    }

    [Fact]
    public void Normalize_PassesThroughTrimmed_NonGuid()
    {
        InstallationIdNormalizer.Normalize("  install-A  ")
            .Should().Be("install-A");
    }
}
