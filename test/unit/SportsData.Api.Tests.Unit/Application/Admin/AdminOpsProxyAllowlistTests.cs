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
    [InlineData("producer", "franchise-seasons/seasonYear/2026/source")]
    [InlineData("producer", "competition/123/metrics")]
    [InlineData("producer", "contests/refresh")]
    [InlineData("producer", "contests")]
    [InlineData("PRODUCER", "Franchise-Seasons/x")]
    [InlineData("provider", "documents/replay")]
    public void AllowedFamilies_PassPerService(string service, string path)
    {
        AdminOpsProxyController.Allowlist.IsAllowed(service, path).Should().BeTrue();
    }

    [Theory]
    [InlineData("producer", "documents/replay", "provider-only family on producer")]
    [InlineData("provider", "franchise-seasons/x", "producer-only family on provider")]
    [InlineData("producer", "hangfire", "ops dashboards are never proxied")]
    [InlineData("producer", "test/outbox", "the deleted test surface stays dead")]
    [InlineData("gateway", "contests/refresh", "unknown service")]
    [InlineData("producer", "", "empty path")]
    [InlineData("producer", "franchise-seasons-admin-delete", "shared textual prefix is not the family — segment boundary required")]
    [InlineData("producer", "contestsX/anything", "no segment boundary after prefix")]
    public void EverythingElse_IsDenied(string service, string path, string because)
    {
        AdminOpsProxyController.Allowlist.IsAllowed(service, path).Should().BeFalse(because);
    }

    // Base ends in /api, mirroring the real ProducerClientConfig ApiUrl — the
    // proxy composes resource-relative paths against the API root.
    private static readonly Uri BaseUri = new("http://producer.internal/api/");

    [Theory]
    [InlineData("contests/../../hangfire", "literal dot-segments normalize OUT of the family")]
    [InlineData("franchise-seasons/../test/outbox", "traversal into the deleted test surface")]
    [InlineData("contests/%2e%2e/%2e%2e/hangfire", "percent-encoded traversal")]
    [InlineData("contests/%252e%252e/hangfire", "double-encoded traversal (residual escape rejected)")]
    [InlineData("http://attacker.internal/contests/refresh", "absolute-URI opPath escapes the base host")]
    [InlineData("//attacker.internal/contests/refresh", "scheme-relative opPath escapes to another host")]
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
                "producer", BaseUri, "contests/ignored/../refresh", "?seasonYear=2026", out var target)
            .Should().BeTrue();

        target.AbsolutePath.Should().Be("/api/contests/refresh");
        target.Query.Should().Be("?seasonYear=2026");
    }
}
