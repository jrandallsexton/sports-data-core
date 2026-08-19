using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Smack.Commands.RateSmackPreview;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Smack.Commands.RateSmackPreview;

public class RateSmackPreviewCommandHandlerTests : SmackTestBase<RateSmackPreviewCommandHandler>
{
    private RateSmackPreviewCommandHandler Sut() => Mocker.CreateInstance<RateSmackPreviewCommandHandler>();

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

        (await sut.ExecuteAsync(Rating(1, "first line"))).IsSuccess.Should().BeTrue();
        (await sut.ExecuteAsync(Rating(4, "revised line"))).IsSuccess.Should().BeTrue();

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
        var result = await Sut().ExecuteAsync(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Situation = nameof(PickSituation.GenericLoss),
            RenderedText = "line",
            Stars = stars
        });

        result.Should().BeOfType<Failure<bool>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task RatePreview_FallbackRating_AllowsNullPhraseId()
    {
        // Rating a fallback marks a bucket that needs lines — PhraseId null is
        // a legitimate training row, not an error.
        var result = await Sut().ExecuteAsync(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Voice = "Smack",
            Situation = nameof(PickSituation.ShutoutLoss),
            PhraseId = null,
            RenderedText = "(standard copy)",
            Stars = 0
        });

        result.IsSuccess.Should().BeTrue();
        (await DataContext.SmackPreviewRatings.SingleAsync()).PhraseId.Should().BeNull();
    }

    [Fact]
    public async Task RatePreview_NumericSituation_IsRejected()
    {
        var result = await Sut().ExecuteAsync(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Voice = "Smack",
            Situation = "999",
            RenderedText = "line",
            Stars = 2
        });

        result.Should().BeOfType<Failure<bool>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task RatePreview_OversizedRenderedText_IsRejectedBeforeTheDatabase()
    {
        // Oversized input must fail at the boundary, not surface as a DB error.
        var result = await Sut().ExecuteAsync(new SmackRatingRequestDto
        {
            PickId = Guid.NewGuid(),
            Voice = "Smack",
            Situation = nameof(PickSituation.GenericLoss),
            RenderedText = new string('r', 401),
            Stars = 2
        });

        result.Should().BeOfType<Failure<bool>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }
}
