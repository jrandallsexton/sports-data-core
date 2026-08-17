using FluentAssertions;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Notification.Application.Dispatching;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Controllers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Controllers;

/// <summary>
/// The Lab's contract is FIDELITY: a preview must run the send path's exact
/// resolution so a rating grades what a user would actually have received.
/// These tests use the real catalog (seeded DbContext), not a mock.
/// </summary>
public class SmackAdminControllerTests : NotificationTestBase<SmackAdminController>
{
    private static readonly DateTime FixedNow = new(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc);

    public SmackAdminControllerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        // Real catalog against the shared in-memory context — fidelity is the
        // point, so the preview tests must not mock resolution.
        Mocker.Use<ISmackPhraseCatalog>(Mocker.CreateInstance<SmackPhraseCatalog>());
    }

    private SmackAdminController Sut() => Mocker.CreateInstance<SmackAdminController>();

    private async Task<SmackPhrase> SeedPhraseAsync(
        PickSituation situation,
        string text,
        bool gambling = false,
        bool active = true)
    {
        var phrase = new SmackPhrase
        {
            Id = Guid.NewGuid(),
            Voice = NotificationVoice.Smack,
            Situation = situation,
            Text = text,
            IsActive = active,
            RequiresGamblingContent = gambling,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };
        DataContext.SmackPhrases.Add(phrase);
        await DataContext.SaveChangesAsync();
        return phrase;
    }

    /// <summary>Straight-up blowout loss (7-35, away pick): BlowoutLoss.</summary>
    private static SmackPreviewPickDto BlowoutLossPick(Guid? pickId = null) => new()
    {
        PickId = pickId ?? Guid.NewGuid(),
        ContestId = Guid.NewGuid(),
        LeagueId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        AwayAbbreviation = "AWY",
        HomeAbbreviation = "HOM",
        AwayScore = 7,
        HomeScore = 35,
        IsCorrect = false,
        PickedIsHome = false,
        LeagueName = "Sluggers",
        Sport = Sport.FootballNcaa
    };

    // ─── Preview ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Preview_ResolvesSituationPhraseAndText()
    {
        var phrase = await SeedPhraseAsync(PickSituation.BlowoutLoss, "{Team} got waxed by {Margin}.");

        var result = await Sut().Preview(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = [BlowoutLossPick()]
        }, CancellationToken.None);

        var previews = (result.Result as OkObjectResult)!.Value as List<SmackPreviewResultDto>;
        previews.Should().HaveCount(1);
        previews![0].Situation.Should().Be(nameof(PickSituation.BlowoutLoss));
        previews[0].PhraseId.Should().Be(phrase.Id);
        previews[0].Text.Should().Be("AWY got waxed by 28.");
        previews[0].UsedStandardFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_EmptyCatalog_FlagsStandardFallback_ButStillLabelsSituation()
    {
        var result = await Sut().Preview(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = [BlowoutLossPick()]
        }, CancellationToken.None);

        var previews = (result.Result as OkObjectResult)!.Value as List<SmackPreviewResultDto>;
        previews![0].UsedStandardFallback.Should().BeTrue();
        previews[0].Text.Should().BeNull();
        previews[0].Situation.Should().Be(nameof(PickSituation.BlowoutLoss),
            "the Lab labels every pick even when no phrase exists yet");
    }

    [Fact]
    public async Task Preview_IsDeterministicForTheSamePick()
    {
        await SeedPhraseAsync(PickSituation.BlowoutLoss, "line a");
        await SeedPhraseAsync(PickSituation.BlowoutLoss, "line b");
        await SeedPhraseAsync(PickSituation.BlowoutLoss, "line c");

        var pick = BlowoutLossPick();
        var first = await PreviewOne(pick);
        for (var i = 0; i < 5; i++)
            (await PreviewOne(pick)).PhraseId.Should().Be(first.PhraseId,
                "a rating must grade the same line a live send would choose");
    }

    [Fact]
    public async Task Preview_StraightUpPick_NeverReceivesGamblingLines()
    {
        // The only BlowoutLoss line is gambling-gated; a straight-up pick
        // (PickedSpread null) must fall back rather than see it.
        await SeedPhraseAsync(PickSituation.BlowoutLoss, "Vegas told you so", gambling: true);

        var preview = await PreviewOne(BlowoutLossPick());

        preview.UsedStandardFallback.Should().BeTrue();
    }

    [Fact]
    public async Task Preview_RejectsOversizedAndEmptyBatches()
    {
        var sut = Sut();

        (await sut.Preview(new SmackPreviewRequestDto { Voice = "Smack", Picks = [] }, CancellationToken.None))
            .Result.Should().BeOfType<BadRequestObjectResult>();

        var oversized = new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = Enumerable.Range(0, 501).Select(_ => BlowoutLossPick()).ToList()
        };
        (await sut.Preview(oversized, CancellationToken.None))
            .Result.Should().BeOfType<BadRequestObjectResult>();
    }

    private async Task<SmackPreviewResultDto> PreviewOne(SmackPreviewPickDto pick)
    {
        var result = await Sut().Preview(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = [pick]
        }, CancellationToken.None);
        return ((result.Result as OkObjectResult)!.Value as List<SmackPreviewResultDto>)![0];
    }

    // ─── Phrases ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePhrase_PersistsAndReturnsDto()
    {
        var result = await Sut().CreatePhrase(new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.ShutoutLoss),
            Text = "  Zero. ZERO.  ",
            Weight = 2,
            Description = "shutout jab"
        }, CancellationToken.None);

        var dto = (result.Result as OkObjectResult)!.Value as SmackPhraseDto;
        dto!.Text.Should().Be("Zero. ZERO.", "text is trimmed");

        var row = await DataContext.SmackPhrases.AsNoTracking().SingleAsync();
        row.Situation.Should().Be(PickSituation.ShutoutLoss);
        row.Weight.Should().Be(2);
    }

    [Theory]
    [InlineData("Smack", "NotASituation", "text", 1)]  // unknown situation
    [InlineData("Sassy", "ShutoutLoss", "text", 1)]    // unknown voice
    [InlineData("Smack", "ShutoutLoss", "", 1)]        // empty text
    [InlineData("Smack", "ShutoutLoss", "text", 0)]    // weight below 1
    public async Task CreatePhrase_RejectsInvalidPayloads(
        string voice, string situation, string text, int weight)
    {
        var result = await Sut().CreatePhrase(new SmackPhraseUpsertDto
        {
            Voice = voice,
            Situation = situation,
            Text = text,
            Weight = weight
        }, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
        (await DataContext.SmackPhrases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task UpdatePhrase_UnknownId_Returns404_AndValidUpdatePersists()
    {
        var sut = Sut();

        (await sut.UpdatePhrase(Guid.NewGuid(), new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "won ugly",
            RowVersion = 0
        }, CancellationToken.None)).Result.Should().BeOfType<NotFoundResult>();

        var phrase = await SeedPhraseAsync(PickSituation.UglyWin, "original");
        await sut.UpdatePhrase(phrase.Id, new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "revised",
            IsActive = false,
            RowVersion = 0 // matches the InMemory store's un-incremented xmin
        }, CancellationToken.None);

        var row = await DataContext.SmackPhrases.AsNoTracking().SingleAsync(p => p.Id == phrase.Id);
        row.Text.Should().Be("revised");
        row.IsActive.Should().BeFalse("deactivation is an update, not a delete");
    }

    // ─── Ratings ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RatePreview_InsertsThenUpsertsOnRerate()
    {
        var pickId = Guid.NewGuid();
        var sut = Sut();

        SmackRatingRequestDto Rating(int stars, string text) => new()
        {
            PickId = pickId,
            ContestId = Guid.NewGuid(),
            LeagueId = Guid.NewGuid(),
            PickerUserId = Guid.NewGuid(),
            Voice = "Smack",
            Situation = nameof(PickSituation.BlowoutLoss),
            PhraseId = Guid.NewGuid(),
            RenderedText = text,
            Stars = stars,
            FactsJson = "{\"margin\":28}"
        };

        (await sut.RatePreview(Rating(1, "first line"), CancellationToken.None))
            .Should().BeOfType<OkResult>();
        (await sut.RatePreview(Rating(4, "revised line"), CancellationToken.None))
            .Should().BeOfType<OkResult>();

        var row = await DataContext.SmackPreviewRatings.AsNoTracking().SingleAsync();
        row.Stars.Should().Be(4, "re-rating overwrites — one current opinion per pick");
        row.RenderedText.Should().Be("revised line");
        row.ModifiedUtc.Should().NotBeNull();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(5)]
    public async Task RatePreview_RejectsStarsOutOfRange(int stars)
    {
        var result = await Sut().RatePreview(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Situation = nameof(PickSituation.GenericLoss),
            RenderedText = "line",
            Stars = stars
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task RatePreview_FallbackRating_AllowsNullPhraseId()
    {
        // Rating a fallback marks a bucket that needs lines — PhraseId null is
        // a legitimate training row, not an error.
        var result = await Sut().RatePreview(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Situation = nameof(PickSituation.ShutoutLoss),
            PhraseId = null,
            RenderedText = "(standard copy)",
            Stars = 0
        }, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
        (await DataContext.SmackPreviewRatings.SingleAsync()).PhraseId.Should().BeNull();
    }

    // ─── CR round: concurrency, enum loopholes, length caps ───────────────

    [Fact]
    public async Task UpdatePhrase_MissingRowVersion_IsRejected()
    {
        var phrase = await SeedPhraseAsync(PickSituation.UglyWin, "original");

        var result = await Sut().UpdatePhrase(phrase.Id, new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "revised"
            // RowVersion deliberately omitted
        }, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task UpdatePhrase_StaleRowVersion_Returns409()
    {
        // A stale editor must get a conflict, never silently clobber a newer
        // edit — the entire reason the entity carries xmin.
        var phrase = await SeedPhraseAsync(PickSituation.UglyWin, "original");

        var result = await Sut().UpdatePhrase(phrase.Id, new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "stale edit",
            RowVersion = 999 // does not match the stored token
        }, CancellationToken.None);

        result.Result.Should().BeOfType<ConflictObjectResult>();
        var storedText = await DataContext.SmackPhrases.AsNoTracking()
            .Where(p => p.Id == phrase.Id)
            .Select(p => p.Text)
            .SingleAsync();
        storedText.Should().Be("original", "the stale write must not have landed");
    }

    [Fact]
    public async Task CreatePhrase_NumericEnumString_IsRejected()
    {
        // Enum.TryParse accepts "999" and would persist an undefined value —
        // the IsDefined guard closes the loophole.
        var result = await Sut().CreatePhrase(new SmackPhraseUpsertDto
        {
            Voice = "999",
            Situation = nameof(PickSituation.ShutoutLoss),
            Text = "line"
        }, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Preview_NumericVoiceString_FallsBackToStandard()
    {
        await SeedPhraseAsync(PickSituation.BlowoutLoss, "should not surface");

        var result = await Sut().Preview(new SmackPreviewRequestDto
        {
            Voice = "999",
            Picks = [BlowoutLossPick()]
        }, CancellationToken.None);

        var previews = (result.Result as OkObjectResult)!.Value as List<SmackPreviewResultDto>;
        previews![0].UsedStandardFallback.Should().BeTrue(
            "an undefined numeric voice must preview as Standard, not consult the catalog");
    }

    [Fact]
    public async Task RatePreview_NumericSituation_IsRejected()
    {
        var result = await Sut().RatePreview(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Situation = "999",
            RenderedText = "line",
            Stars = 2
        }, CancellationToken.None);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task LengthCaps_AreEnforcedBeforeTheDatabase()
    {
        // Oversized input must 400 at the boundary, not surface as a DB error.
        var longDescription = new string('d', 257);
        (await Sut().CreatePhrase(new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.GenericLoss),
            Text = "line",
            Description = longDescription
        }, CancellationToken.None)).Result.Should().BeOfType<BadRequestObjectResult>();

        var longRendered = new string('r', 401);
        (await Sut().RatePreview(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Situation = nameof(PickSituation.GenericLoss),
            RenderedText = longRendered,
            Stars = 2
        }, CancellationToken.None)).Should().BeOfType<BadRequestObjectResult>();
    }
}
