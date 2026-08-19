using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Smack.Commands.CreateSmackPhrase;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Smack.Commands.CreateSmackPhrase;

public class CreateSmackPhraseCommandHandlerTests : SmackTestBase<CreateSmackPhraseCommandHandler>
{
    private CreateSmackPhraseCommandHandler Sut() => Mocker.CreateInstance<CreateSmackPhraseCommandHandler>();

    [Fact]
    public async Task CreatePhrase_PersistsAndReturnsDto()
    {
        var result = await Sut().ExecuteAsync(new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.ShutoutLoss),
            Text = "  Zero. ZERO.  ",
            Weight = 2,
            Description = "shutout jab"
        });

        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("Zero. ZERO.", "text is trimmed");

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
        var result = await Sut().ExecuteAsync(new SmackPhraseUpsertDto
        {
            Voice = voice,
            Situation = situation,
            Text = text,
            Weight = weight
        });

        result.Should().BeOfType<Failure<SmackPhraseDto>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
        (await DataContext.SmackPhrases.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task CreatePhrase_NumericEnumString_IsRejected()
    {
        // Enum.TryParse accepts "999" and would persist an undefined value —
        // the IsDefined guard closes the loophole.
        var result = await Sut().ExecuteAsync(new SmackPhraseUpsertDto
        {
            Voice = "999",
            Situation = nameof(PickSituation.ShutoutLoss),
            Text = "line"
        });

        result.Should().BeOfType<Failure<SmackPhraseDto>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task CreatePhrase_OversizedDescription_IsRejectedBeforeTheDatabase()
    {
        // Oversized input must fail at the boundary, not surface as a DB error.
        var result = await Sut().ExecuteAsync(new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.GenericLoss),
            Text = "line",
            Description = new string('d', 257)
        });

        result.Should().BeOfType<Failure<SmackPhraseDto>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }
}
