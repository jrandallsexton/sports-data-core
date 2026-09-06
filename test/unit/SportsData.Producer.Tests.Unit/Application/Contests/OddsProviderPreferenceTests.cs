using FluentAssertions;

using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

public class OddsProviderPreferenceTests
{
    // Fixed timestamp: only HasValue matters to the policy, and the repo
    // convention bans direct clock access in tests either way.
    private static readonly DateTime FinalizedAt = new(2026, 9, 5, 23, 0, 0, DateTimeKind.Utc);

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
            FinalizedUtc = finalized ? FinalizedAt : null,
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
    public void ModernContest_ResolvesWithinDisplayedSet_ForeignLineNeverFinalizes()
    {
        // Vortex round 2 (PR #732): the display/scoring stack reads only
        // 58/100. When a displayed-set row exists - even spreadless - a
        // foreign book's line must NOT finalize the contest: the product
        // showed no line and picks were scored straight-up, so an ATS
        // winner from a book nobody saw would contradict the pick grades.
        var dkSpreadless = Odds("100", "DraftKings", spread: null);
        var caesarsWithSpread = Odds("31", "Caesars", spread: -6.5m);

        OddsProviderPreference.SelectPrimary(new[] { caesarsWithSpread, dkSpreadless })
            .Should().BeSameAs(dkSpreadless);
    }

    [Fact]
    public void ModernContest_MirrorsSqlLateralExactly_ProviderOrderSpreadIgnored()
    {
        // Vortex round 3 (PR #732): the SQL laterals resolve by provider
        // order LIMIT 1 with spread never considered, and the denorm row
        // must be the LITERAL row the display/scoring stack reads. So a
        // spreadless 58 shadows a spread-carrying 100 - ATS stays null,
        // matching the no-line-shown, scored-straight-up experience.
        // (Flipping this to spread-first starts with the SQL laterals on
        // both services, then this policy, in the same change.)
        var espnSpreadless = Odds("58", "ESPN BET", spread: null);
        var dkWithSpread = Odds("100", "DraftKings", spread: -35.5m);

        OddsProviderPreference.SelectPrimary(new[] { dkWithSpread, espnSpreadless })
            .Should().BeSameAs(espnSpreadless);
    }

    [Fact]
    public void HistoricalContest_NoDisplayedRows_FallsBackToSpreadCarryingEraBook()
    {
        // The pre-2023 corpus (10,588 contests measured 2026-09-06) carries
        // spreads only from era books - historical re-finalization must
        // preserve their ATS record.
        var spreadless = Odds("31", "Caesars", spread: null);
        var withSpread = Odds("25", "Westgate", spread: -7m);

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
