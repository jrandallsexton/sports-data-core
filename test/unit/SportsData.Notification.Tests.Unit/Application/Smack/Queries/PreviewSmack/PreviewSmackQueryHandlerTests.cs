using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Smack.Queries.PreviewSmack;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Smack.Queries.PreviewSmack;

public class PreviewSmackQueryHandlerTests : SmackTestBase<PreviewSmackQueryHandler>
{
    public PreviewSmackQueryHandlerTests()
    {
        // Real catalog against the shared in-memory context — fidelity is the
        // point, so these tests must not mock resolution.
        Mocker.Use<ISmackPhraseCatalog>(Mocker.CreateInstance<SmackPhraseCatalog>());
    }

    private PreviewSmackQueryHandler Sut() => Mocker.CreateInstance<PreviewSmackQueryHandler>();

    [Fact]
    public async Task Preview_ResolvesSituationPhraseAndText()
    {
        var phrase = await SeedPhraseAsync(PickSituation.BlowoutLoss, "{Team} got waxed by {Margin}.");

        var result = await Sut().ExecuteAsync(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = [BlowoutLossPick()]
        });

        result.IsSuccess.Should().BeTrue();
        var previews = result.Value;
        previews.Should().HaveCount(1);
        previews[0].Situation.Should().Be(nameof(PickSituation.BlowoutLoss));
        previews[0].PhraseId.Should().Be(phrase.Id);
        previews[0].Text.Should().Be("AWY got waxed by 28.");
        previews[0].UsedStandardFallback.Should().BeFalse();
    }

    [Fact]
    public async Task Preview_EmptyCatalog_FlagsStandardFallback_ButStillLabelsSituation()
    {
        var result = await Sut().ExecuteAsync(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = [BlowoutLossPick()]
        });

        var previews = result.Value;
        previews[0].UsedStandardFallback.Should().BeTrue();
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

        var empty = await sut.ExecuteAsync(new SmackPreviewRequestDto { Voice = "Smack", Picks = [] });
        empty.Should().BeOfType<Failure<List<SmackPreviewResultDto>>>();
        empty.Status.Should().Be(ResultStatus.BadRequest);

        var oversized = await sut.ExecuteAsync(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = Enumerable.Range(0, 501).Select(_ => BlowoutLossPick()).ToList()
        });
        oversized.Should().BeOfType<Failure<List<SmackPreviewResultDto>>>();
        oversized.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task Preview_NumericVoiceString_FallsBackToStandard()
    {
        await SeedPhraseAsync(PickSituation.BlowoutLoss, "should not surface");

        var result = await Sut().ExecuteAsync(new SmackPreviewRequestDto
        {
            Voice = "999",
            Picks = [BlowoutLossPick()]
        });

        result.Value[0].UsedStandardFallback.Should().BeTrue(
            "an undefined numeric voice must preview as Standard, not consult the catalog");
    }

    private async Task<SmackPreviewResultDto> PreviewOne(SmackPreviewPickDto pick)
    {
        var result = await Sut().ExecuteAsync(new SmackPreviewRequestDto
        {
            Voice = "Smack",
            Picks = [pick]
        });
        return result.Value[0];
    }
}
