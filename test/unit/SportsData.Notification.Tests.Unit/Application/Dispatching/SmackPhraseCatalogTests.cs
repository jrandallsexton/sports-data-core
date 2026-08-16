using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Dispatching;

/// <summary>
/// Selection must be deterministic: a redelivery cannot produce a different
/// line, and the choice has to be reproducible in a test without injecting an
/// RNG. See docs/features/smackbot-voice.md.
/// </summary>
public class SmackPhraseCatalogTests : NotificationTestBase<SmackPhraseCatalog>
{
    private static readonly Guid PickId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    private async Task SeedAsync(params SmackPhrase[] phrases)
    {
        DataContext.SmackPhrases.AddRange(phrases);
        await DataContext.SaveChangesAsync();
    }

    private Task<string> ResolveAsync(Guid pickId, bool allowGambling = true)
        => Mocker.CreateInstance<SmackPhraseCatalog>()
            .TryResolveAsync(Loss(pickId), NotificationVoice.Smack, allowGambling);

    private static UserPickScored Loss(Guid pickId)
        => new(
            Guid.NewGuid(), null, Guid.NewGuid(), pickId,
            null, null, "NYY", "BOS", 24, 14,
            IsCorrect: false, PickedIsHome: true, PickedSpread: null,
            Guid.NewGuid(), "Sluggers", Sport.FootballNcaa, 2026,
            Guid.NewGuid(), Guid.NewGuid());

    private static SmackPhrase Phrase(string text, int weight = 1, Guid? id = null)
        => new()
        {
            Id = id ?? Guid.NewGuid(),
            Voice = NotificationVoice.Smack,
            Situation = PickSituation.GenericLoss,
            Text = text,
            Weight = weight
        };

    [Fact]
    public async Task EmptyCatalog_ReturnsNullSoCallerUsesStandardCopy()
    {
        // The schema ships before the content exists, so this is a supported
        // state — not an error.
        (await ResolveAsync(PickId)).Should().BeNull();
    }

    [Fact]
    public async Task StandardVoice_NeverConsultsTheCatalog()
    {
        await SeedAsync(Phrase("should not be used"));

        var result = await Mocker.CreateInstance<SmackPhraseCatalog>()
            .TryResolveAsync(Loss(PickId), NotificationVoice.Standard, allowGamblingContent: true);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Selection_IsStableForTheSamePick()
    {
        await SeedAsync(Phrase("a"), Phrase("b"), Phrase("c"), Phrase("d"));

        var first = await ResolveAsync(PickId);

        // Same pick must always yield the same line — what makes redelivery
        // safe and the behaviour reproducible.
        for (var i = 0; i < 10; i++)
            (await ResolveAsync(PickId)).Should().Be(first);
    }

    [Fact]
    public async Task Selection_VariesAcrossPicks()
    {
        // Determinism must not collapse into "always the same line" — two
        // users losing the same way should not read an identical taunt.
        await SeedAsync(Phrase("a"), Phrase("b"), Phrase("c"), Phrase("d"));

        var chosen = new HashSet<string>();
        for (var i = 0; i < 100; i++)
            chosen.Add(await ResolveAsync(Guid.NewGuid()));

        chosen.Should().HaveCountGreaterThan(1, "a single line for every pick would defeat the library");
    }

    [Fact]
    public async Task Weighting_SkewsSelectionTowardHeavierLines()
    {
        await SeedAsync(Phrase("heavy", weight: 9), Phrase("light"));

        var heavyCount = 0;
        for (var i = 0; i < 400; i++)
            if (await ResolveAsync(Guid.NewGuid()) == "heavy") heavyCount++;

        // ~90% expected; a loose band keeps it non-flaky while still proving
        // weighting is honoured.
        heavyCount.Should().BeInRange(300, 396);
    }

    [Fact]
    public async Task GamblingFilter_ExcludesLineReferencingPhrases()
    {
        // A StraightUp player who hid gambling content must never be told what
        // Vegas thought.
        await SeedAsync(
            Phrase("safe line"),
            new SmackPhrase
            {
                Id = Guid.NewGuid(),
                Voice = NotificationVoice.Smack,
                Situation = PickSituation.GenericLoss,
                Text = "Vegas said so",
                RequiresGamblingContent = true
            });

        for (var i = 0; i < 20; i++)
        {
            (await ResolveAsync(Guid.NewGuid(), allowGambling: false))
                .Should().Be("safe line");
        }
    }

    [Fact]
    public void Formatter_ResolvesTokensFromThePickedSidePerspective()
    {
        var msg = new UserPickScored(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            AwayName: null, HomeName: null,
            AwayAbbreviation: "NYY", HomeAbbreviation: "BOS",
            AwayScore: 2, HomeScore: 9,
            IsCorrect: true, PickedIsHome: true, PickedSpread: -6.5,
            LeagueId: Guid.NewGuid(), LeagueName: "Sluggers",
            Sport: Sport.BaseballMlb, SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(), CausationId: Guid.NewGuid());

        var result = SmackPhraseFormatter.Format(
            "{Team} {Score}, {Opponent} {OpponentScore} — by {Margin} in {League} ({Line})", msg);

        result.Should().Be("BOS 9, NYY 2 — by 7 in Sluggers (6.5)");
    }

    [Fact]
    public void Formatter_LeavesUnknownTokensAloneRatherThanThrowing()
    {
        var msg = new UserPickScored(
            Guid.NewGuid(), null, Guid.NewGuid(), Guid.NewGuid(),
            null, null, "NYY", "BOS", 2, 9,
            true, true, null,
            Guid.NewGuid(), "Sluggers", Sport.BaseballMlb, 2026,
            Guid.NewGuid(), Guid.NewGuid());

        // A typo in an operator-authored line should look odd, not drop the
        // notification. {Line} is also unresolved here (no spread sent).
        var result = SmackPhraseFormatter.Format("{Team} {Typo} {Line}", msg);

        result.Should().Be("BOS {Typo} {Line}");
    }
}
