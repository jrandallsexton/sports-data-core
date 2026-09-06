using FluentAssertions;

using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

public class OddsProviderPreferenceTests
{
    private static CompetitionOdds Odds(
        string providerId, string providerName, decimal? spread, bool finalized = true)
        => new()
        {
            Id = Guid.NewGuid(),
            CompetitionId = Guid.NewGuid(),
            ProviderRef = new Uri($"http://sports.core.api.espn.com/v2/providers/{providerId}"),
            ProviderId = providerId,
            ProviderName = providerName,
            Spread = spread,
            FinalizedUtc = finalized ? DateTime.UtcNow : null,
        };

    [Fact]
    public void KentStateRegression_LiveOddsRowNeverWins_PregameDraftKingsSelected()
    {
        // 2026-09-05: KENT 0 @ SC 57, DK -35.5. The live-odds row (200) has
        // no spread; picking it graded the game a push. The pregame
        // DraftKings row (100) must win regardless of collection order.
        var live = Odds("200", "DraftKings - Live Odds", spread: null);
        var pregame = Odds("100", "DraftKings", spread: -35.5m);

        OddsProviderPreference.SelectPrimary(new[] { live, pregame })
            .Should().BeSameAs(pregame);
        OddsProviderPreference.SelectPrimary(new[] { pregame, live })
            .Should().BeSameAs(pregame);
    }

    [Fact]
    public void EspnBetPreferredOverDraftKings()
    {
        var espnBet = Odds("58", "ESPN BET", spread: -3.5m);
        var dk = Odds("100", "DraftKings", spread: -3m);

        OddsProviderPreference.SelectPrimary(new[] { dk, espnBet })
            .Should().BeSameAs(espnBet);
    }

    [Fact]
    public void OnlyLiveOddsRows_ReturnsNull_NeverALiveRow()
    {
        // Rule 1: live odds are NEVER primary - a null here means callers
        // leave the Contest-level denorm alone instead of writing garbage.
        var liveDk = Odds("200", "DraftKings - Live Odds", spread: -20m);
        var liveEspn = Odds("59", "ESPN BET - Live Odds", spread: -21m);

        OddsProviderPreference.SelectPrimary(new[] { liveDk, liveEspn })
            .Should().BeNull();
    }

    [Fact]
    public void UnfinalizedRowsAreIgnored()
    {
        var unfinalized = Odds("58", "ESPN BET", spread: -3.5m, finalized: false);
        var finalized = Odds("100", "DraftKings", spread: -3m);

        OddsProviderPreference.SelectPrimary(new[] { unfinalized, finalized })
            .Should().BeSameAs(finalized);
    }

    [Fact]
    public void UnknownBooks_RowWithSpreadBeatsRowWithout()
    {
        var spreadless = Odds("31", "Caesars", spread: null);
        var withSpread = Odds("47", "MGM", spread: -7m);

        OddsProviderPreference.SelectPrimary(new[] { spreadless, withSpread })
            .Should().BeSameAs(withSpread);
    }

    [Fact]
    public void EmptyOrNull_ReturnsNull()
    {
        OddsProviderPreference.SelectPrimary(null).Should().BeNull();
        OddsProviderPreference.SelectPrimary(Array.Empty<CompetitionOdds>()).Should().BeNull();
    }
}
