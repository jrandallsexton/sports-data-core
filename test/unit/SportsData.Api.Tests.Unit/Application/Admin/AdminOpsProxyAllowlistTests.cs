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
    public void EverythingElse_IsDenied(string service, string path, string because)
    {
        AdminOpsProxyController.Allowlist.IsAllowed(service, path).Should().BeFalse(because);
    }
}
