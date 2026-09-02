using FluentAssertions;

using SportsData.Api.Application.Common.Moderation;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Common.Moderation;

/// <summary>
/// The load-bearing guarantee here is the Scunthorpe suite: this filter
/// matches WHOLE WORDS ONLY, so real surnames containing banned substrings
/// must always pass. The operator's own surname is Sexton — that test is not
/// hypothetical, and it must never regress to substring matching.
/// </summary>
public class ProfanityPolicyTests
{
    // ── Scunthorpe suite: real names that substring matching would block ────

    [Theory]
    [InlineData("Sexton")]              // the operator's surname
    [InlineData("Randall Sexton")]
    [InlineData("S3xton")]              // leet-normalizes to "sexton" — still a whole word
    [InlineData("Dickinson")]
    [InlineData("Cummings")]
    [InlineData("Hancock")]
    [InlineData("Scunthorpe United")]   // the namesake
    [InlineData("Cassandra")]           // contains "ass"
    [InlineData("Matt Titsworth")]      // contains "tits"
    public void RealNames_AreNotProfane(string candidate)
    {
        ProfanityPolicy.ContainsProfanity(candidate).Should().BeFalse(
            $"'{candidate}' is a legitimate name and whole-word matching must let it through");
    }

    // ── Plain profanity as a standalone token ───────────────────────────────

    [Theory]
    [InlineData("fuck")]
    [InlineData("total asshole")]
    [InlineData("The Assholes")]
    [InlineData("Shit Show")]
    [InlineData("league of sluts")]
    public void ProfaneTokens_AreBlocked(string candidate)
    {
        ProfanityPolicy.ContainsProfanity(candidate).Should().BeTrue();
    }

    // ── Evasion that normalization must catch ───────────────────────────────

    [Theory]
    [InlineData("f.u.c.k")]             // collapsed letters equal a banned word
    [InlineData("f u c k")]
    [InlineData("F-U-C-K")]
    [InlineData("a$$hole")]             // $→s: token "asshole"
    [InlineData("sh1thead")]            // 1→i: token "shithead"
    [InlineData("b!tch")]               // !→i
    [InlineData("N1gger")]              // leet slur
    [InlineData("c.u.n.t")]
    public void LeetAndSeparatorEvasion_IsBlocked(string candidate)
    {
        ProfanityPolicy.ContainsProfanity(candidate).Should().BeTrue(
            $"'{candidate}' normalizes to a banned whole word");
    }

    // ── Documented recall boundary (deliberate, not a bug) ──────────────────

    [Fact]
    public void GluedCompounds_NotOnTheList_Pass()
    {
        // "bigslut" is one token and equals no listed word. Whole-word
        // matching trades this recall for the precision that keeps real
        // names safe; the fix for a compound that shows up in the wild is
        // adding it to the wordlist, never switching to substring matching.
        ProfanityPolicy.ContainsProfanity("BigSlut69").Should().BeFalse();
    }

    // ── Clean input ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Gridiron Gurus")]
    [InlineData("Saturday Slate")]
    [InlineData("The 12th Man")]
    [InlineData("Touchdown Toms")]
    [InlineData("José's League")]       // diacritics must not break normalization
    public void CleanNames_Pass(string candidate)
    {
        ProfanityPolicy.ContainsProfanity(candidate).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NullOrWhitespace_IsNotProfane(string? candidate)
    {
        // Required-ness is a separate validation rule's job.
        ProfanityPolicy.ContainsProfanity(candidate).Should().BeFalse();
    }
}
