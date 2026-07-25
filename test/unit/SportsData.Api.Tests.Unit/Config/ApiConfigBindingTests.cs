using FluentAssertions;

using Microsoft.Extensions.Configuration;

using SportsData.Api.Config;

using Xunit;

namespace SportsData.Api.Tests.Unit.Config;

public class ApiConfigBindingTests
{
    [Fact]
    public void LeagueCreationOpensUtc_BindsFromHierarchicalKeys_LikeScalarProps()
    {
        // Mimics how Azure App Configuration delivers keys into IConfiguration:
        // colon-delimited hierarchical keys under the ApiConfig section. A scalar
        // prop (BaseUrl) and a Dictionary<string,string> prop should both bind.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SportsData.Api:ApiConfig:BaseUrl"] = "https://api.test",
                ["SportsData.Api:ApiConfig:UserIdSystem"] = Guid.NewGuid().ToString(),
                ["SportsData.Api:ApiConfig:LeagueCreationOpensUtc:FootballNcaa"] = "2026-08-17T00:00:00Z",
                ["SportsData.Api:ApiConfig:LeagueCreationOpensUtc:FootballNfl"] = "2026-09-01T00:00:00Z",
            })
            .Build();

        var apiConfig = configuration.GetSection("SportsData.Api:ApiConfig").Get<ApiConfig>();

        apiConfig.Should().NotBeNull();
        apiConfig!.BaseUrl.Should().Be("https://api.test");
        apiConfig.LeagueCreationOpensUtc.Should().HaveCount(2);
        apiConfig.LeagueCreationOpensUtc.Should().ContainKey("FootballNcaa");
        apiConfig.LeagueCreationOpensUtc["FootballNcaa"].Should().Be("2026-08-17T00:00:00Z");
    }
}
