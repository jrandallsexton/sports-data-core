using FluentAssertions;

using SportsData.Api.Application.Admin;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin;

/// <summary>
/// The allowlist IS the security boundary of the ops proxy — with a valid
/// admin token, only these path families are reachable on the internal
/// services. Deny-by-default for everything else.
/// </summary>
public class AdminOpsProxyAllowlistTests
{
    [Theory]
    [InlineData("producer", "api/franchise-seasons/seasonYear/2026/source")]
    [InlineData("producer", "api/competition/123/metrics")]
    [InlineData("producer", "api/contests/refresh")]
    [InlineData("producer", "api/contests")]
    [InlineData("PRODUCER", "API/Franchise-Seasons/x")]
    [InlineData("provider", "api/documents/replay")]
    public void AllowedFamilies_PassPerService(string service, string path)
    {
        AdminOpsProxyController.Allowlist.IsAllowed(service, path).Should().BeTrue();
    }

    [Theory]
    [InlineData("producer", "api/documents/replay", "provider-only family on producer")]
    [InlineData("provider", "api/franchise-seasons/x", "producer-only family on provider")]
    [InlineData("producer", "hangfire", "ops dashboards are never proxied")]
    [InlineData("producer", "api/test/outbox", "the deleted test surface stays dead")]
    [InlineData("gateway", "api/contests/refresh", "unknown service")]
    [InlineData("producer", "", "empty path")]
    [InlineData("producer", "api/franchise-seasons-admin-delete", "shared textual prefix is not the family — segment boundary required")]
    [InlineData("producer", "api/contestsX/anything", "no segment boundary after prefix")]
    public void EverythingElse_IsDenied(string service, string path, string because)
    {
        AdminOpsProxyController.Allowlist.IsAllowed(service, path).Should().BeFalse(because);
    }

    private static readonly Uri BaseUri = new("http://producer.internal/");

    [Theory]
    [InlineData("api/contests/../../hangfire", "literal dot-segments normalize OUT of the family")]
    [InlineData("api/franchise-seasons/../test/outbox", "traversal into the deleted test surface")]
    [InlineData("api/contests/%2e%2e/%2e%2e/hangfire", "percent-encoded traversal")]
    [InlineData("api/contests/%252e%252e/hangfire", "double-encoded traversal (residual escape rejected)")]
    public void TraversalPaths_AreDenied_AfterCanonicalization(string opPath, string because)
    {
        AdminOpsProxyController.Allowlist
            .TryResolveAllowedTarget("producer", BaseUri, opPath, string.Empty, out _)
            .Should().BeFalse(because);
    }

    [Fact]
    public void InFamilyDotSegments_NormalizeAndStayAllowed()
    {
        // Normalization that stays INSIDE the family is fine — the check is
        // on the canonical destination, not on cosmetic path shape.
        AdminOpsProxyController.Allowlist
            .TryResolveAllowedTarget(
                "producer", BaseUri, "api/contests/ignored/../refresh", "?seasonYear=2026", out var target)
            .Should().BeTrue();

        target.AbsolutePath.Should().Be("/api/contests/refresh");
        target.Query.Should().Be("?seasonYear=2026");
    }
}
