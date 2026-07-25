using System.Globalization;
using System.Linq;

using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Config;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues;

public class LeagueCreationAvailabilityTests
{
    private static readonly DateTime Now = new(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);

    private static LeagueCreationAvailability Build(Dictionary<Sport, DateTime> gates)
    {
        var config = new ApiConfig
        {
            BaseUrl = "https://api.test",
            UserIdSystem = Guid.NewGuid(),
            // Mirror how AppConfig delivers the gate: a string→string map keyed by
            // Sport name, with an ISO-8601 value. "o" round-trips the Kind (…Z for
            // Utc, bare for Unspecified) so the service's parsing is exercised.
            LeagueCreationOpensUtc = gates.ToDictionary(
                kvp => kvp.Key.ToString(),
                kvp => kvp.Value.ToString("o", CultureInfo.InvariantCulture)),
        };

        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(x => x.UtcNow()).Returns(Now);

        return new LeagueCreationAvailability(Options.Create(config), clock.Object);
    }

    [Fact]
    public void GetOpensUtc_ReturnsInstant_WhenSportGatedInFuture()
    {
        var opens = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc);
        var sut = Build(new() { [Sport.FootballNcaa] = opens });

        sut.GetOpensUtc(Sport.FootballNcaa).Should().Be(opens);
    }

    [Fact]
    public void GetOpensUtc_ReturnsNull_WhenSportAbsent()
    {
        var sut = Build(new() { [Sport.FootballNcaa] = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc) });

        sut.GetOpensUtc(Sport.FootballNfl).Should().BeNull();
    }

    [Fact]
    public void GetOpensUtc_ReturnsNull_WhenGateAlreadyElapsed()
    {
        var sut = Build(new() { [Sport.FootballNcaa] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });

        sut.GetOpensUtc(Sport.FootballNcaa).Should().BeNull();
    }

    [Fact]
    public void GetOpensUtc_TreatsUnspecifiedConfigInstantAsUtc()
    {
        // Config binding yields Kind=Unspecified; the service must compare by the
        // authored UTC instant, not shift it by the host timezone.
        var opens = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Unspecified);
        var sut = Build(new() { [Sport.FootballNcaa] = opens });

        var result = sut.GetOpensUtc(Sport.FootballNcaa);

        result.Should().NotBeNull();
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
        result.Value.Should().Be(DateTime.SpecifyKind(opens, DateTimeKind.Utc));
    }

    [Fact]
    public void GetActiveGates_ReturnsOnlyFutureGates_EarliestFirst()
    {
        var sut = Build(new()
        {
            [Sport.FootballNcaa] = new DateTime(2026, 8, 17, 0, 0, 0, DateTimeKind.Utc), // future
            [Sport.FootballNfl] = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),   // past -> excluded
            [Sport.BaseballMlb] = new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),   // future, earlier
        });

        var gates = sut.GetActiveGates();

        gates.Should().HaveCount(2);
        gates[0].Sport.Should().Be(nameof(Sport.BaseballMlb));
        gates[0].OpensUtc.Should().Be(new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));
        gates[1].Sport.Should().Be(nameof(Sport.FootballNcaa));
    }

    [Fact]
    public void GetActiveGates_Empty_WhenNoGatesConfigured()
    {
        Build(new()).GetActiveGates().Should().BeEmpty();
    }
}
