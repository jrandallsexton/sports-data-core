using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Notification.Dtos;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Application.Smack.Commands.UpdateSmackPhrase;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Smack.Commands.UpdateSmackPhrase;

public class UpdateSmackPhraseCommandHandlerTests : SmackTestBase<UpdateSmackPhraseCommandHandler>
{
    private UpdateSmackPhraseCommandHandler Sut() => Mocker.CreateInstance<UpdateSmackPhraseCommandHandler>();

    [Fact]
    public async Task UpdatePhrase_UnknownId_IsNotFound_AndValidUpdatePersists()
    {
        var sut = Sut();

        var missing = await sut.ExecuteAsync(Guid.NewGuid(), new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "won ugly",
            RowVersion = 0
        });
        missing.Should().BeOfType<Failure<SmackPhraseDto>>();
        missing.Status.Should().Be(ResultStatus.NotFound);

        var phrase = await SeedPhraseAsync(PickSituation.UglyWin, "original");
        await sut.ExecuteAsync(phrase.Id, new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "revised",
            IsActive = false,
            RowVersion = 0 // matches the InMemory store's un-incremented xmin
        });

        var row = await DataContext.SmackPhrases.AsNoTracking().SingleAsync(p => p.Id == phrase.Id);
        row.Text.Should().Be("revised");
        row.IsActive.Should().BeFalse("deactivation is an update, not a delete");
    }

    [Fact]
    public async Task UpdatePhrase_MissingRowVersion_IsRejected()
    {
        var phrase = await SeedPhraseAsync(PickSituation.UglyWin, "original");

        var result = await Sut().ExecuteAsync(phrase.Id, new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "revised"
            // RowVersion deliberately omitted
        });

        result.Should().BeOfType<Failure<SmackPhraseDto>>();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task UpdatePhrase_StaleRowVersion_IsConflict()
    {
        // A stale editor must get a conflict, never silently clobber a newer
        // edit — the entire reason the entity carries xmin.
        var phrase = await SeedPhraseAsync(PickSituation.UglyWin, "original");

        var result = await Sut().ExecuteAsync(phrase.Id, new SmackPhraseUpsertDto
        {
            Voice = "Smack",
            Situation = nameof(PickSituation.UglyWin),
            Text = "stale edit",
            RowVersion = 999 // does not match the stored token
        });

        result.Should().BeOfType<Failure<SmackPhraseDto>>();
        result.Status.Should().Be(ResultStatus.Conflict);
        var storedText = await DataContext.SmackPhrases.AsNoTracking()
            .Where(p => p.Id == phrase.Id)
            .Select(p => p.Text)
            .SingleAsync();
        storedText.Should().Be("original", "the stale write must not have landed");
    }
}
