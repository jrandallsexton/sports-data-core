using FluentAssertions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.MiniGames;
using SportsData.Api.Application.MiniGames.Commands.SubmitMiniGameResults;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using System.Text.Json;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.MiniGames.Commands.SubmitMiniGameResults;

public class SubmitMiniGameResultsCommandHandlerTests : ApiTestBase<SubmitMiniGameResultsCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid UserId = Guid.NewGuid();

    public SubmitMiniGameResultsCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IValidator<SubmitMiniGameResultsCommand>>(new SubmitMiniGameResultsCommandValidator());
        Mocker.Use<IValidator<MiniGameResultSubmission>>(new MiniGameResultSubmissionValidator());
    }

    private static MiniGameResultSubmission Win(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        GameKey = MiniGameKeys.FootballCards,
        Outcome = MiniGameOutcome.Win,
        Score = 21,
        OpponentScore = 14,
        DurationSeconds = 480,
        GameVersion = "1",
        Seed = "abc123",
        PlayedUtc = FixedNow.AddHours(-1)
    };

    private Task<Result<SubmitMiniGameResultsResponse>> Submit(Guid userId, params MiniGameResultSubmission[] results) =>
        Mocker.CreateInstance<SubmitMiniGameResultsCommandHandler>()
            .ExecuteAsync(userId, new SubmitMiniGameResultsCommand { Results = results.ToList() });

    [Fact]
    public async Task Execute_StoresResults_WithServerReceiveTime()
    {
        var item = Win() with { Detail = JsonDocument.Parse("""{"ot":1}""").RootElement };

        var result = await Submit(UserId, item);

        result.IsSuccess.Should().BeTrue();
        result.Value.Accepted.Should().Equal(item.Id);

        DataContext.ChangeTracker.Clear();
        var row = await DataContext.MiniGameResults.SingleAsync();
        row.Id.Should().Be(item.Id);
        row.UserId.Should().Be(UserId);
        row.Outcome.Should().Be(MiniGameOutcome.Win);
        row.Score.Should().Be(21);
        row.OpponentScore.Should().Be(14);
        row.PlayedUtc.Should().Be(item.PlayedUtc);
        row.CreatedUtc.Should().Be(FixedNow);
        row.Detail.Should().Be("""{"ot":1}""");
    }

    [Fact]
    public async Task Execute_ResubmittedId_IsDuplicate_NotInsertedTwice()
    {
        var item = Win();
        await Submit(UserId, item);

        var result = await Submit(UserId, item);

        result.Value.Accepted.Should().BeEmpty();
        result.Value.Duplicate.Should().Equal(item.Id);
        (await DataContext.MiniGameResults.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_SameIdTwiceInOneBatch_FirstWins_ReportedOnce()
    {
        var item = Win();

        var result = await Submit(UserId, item, item with { Score = 35 });

        result.Value.Accepted.Should().Equal(item.Id);
        result.Value.Duplicate.Should().BeEmpty();
        DataContext.ChangeTracker.Clear();
        (await DataContext.MiniGameResults.SingleAsync()).Score.Should().Be(21);
    }

    [Fact]
    public async Task Execute_InvalidItem_IsRejected_WithoutBlockingTheRest()
    {
        var good = Win();
        var unknownGame = Win() with { GameKey = "tic-tac-toe" };
        var wrongOutcome = Win() with { Outcome = MiniGameOutcome.Loss };
        var negative = Win() with { Score = -1 };
        var emptyId = Win(Guid.Empty);

        var result = await Submit(UserId, good, unknownGame, wrongOutcome, negative, emptyId);

        result.IsSuccess.Should().BeTrue();
        result.Value.Accepted.Should().Equal(good.Id);
        result.Value.Rejected.Should().BeEquivalentTo(
            [unknownGame.Id, wrongOutcome.Id, negative.Id, Guid.Empty]);
        (await DataContext.MiniGameResults.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_InvalidCopyThenValidCopy_StoresTheValidOne_ReportedOnce()
    {
        var id = Guid.NewGuid();
        var invalid = Win(id) with { Score = -1 };
        var valid = Win(id);

        var result = await Submit(UserId, invalid, valid);

        result.Value.Accepted.Should().Equal(id);
        result.Value.Duplicate.Should().BeEmpty();
        result.Value.Rejected.Should().BeEmpty();
        (await DataContext.MiniGameResults.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_DeviceClockAhead_IsAccepted_AndClampedToServerTime()
    {
        // Rejecting would lose the game for good: the client drops rejected results.
        var item = Win() with { PlayedUtc = FixedNow.AddHours(3) };

        var result = await Submit(UserId, item);

        result.Value.Accepted.Should().Equal(item.Id);
        DataContext.ChangeTracker.Clear();
        (await DataContext.MiniGameResults.SingleAsync()).PlayedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Execute_OverTheDailyCap_RejectsTheExcess()
    {
        // Already at the cap minus one inside the last 24 hours; one older row doesn't count.
        var stored = Enumerable.Range(0, SubmitMiniGameResultsCommandHandler.MaxResultsPerDay - 1)
            .Select(_ => new MiniGameResult
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                GameKey = MiniGameKeys.FootballCards,
                Outcome = MiniGameOutcome.Win,
                Score = 7,
                GameVersion = "1",
                PlayedUtc = FixedNow.AddHours(-1),
                CreatedUtc = FixedNow.AddHours(-1),
                CreatedBy = UserId
            })
            .Append(new MiniGameResult
            {
                Id = Guid.NewGuid(),
                UserId = UserId,
                GameKey = MiniGameKeys.FootballCards,
                Outcome = MiniGameOutcome.Win,
                Score = 7,
                GameVersion = "1",
                PlayedUtc = FixedNow.AddDays(-2),
                CreatedUtc = FixedNow.AddDays(-2),
                CreatedBy = UserId
            });
        await DataContext.MiniGameResults.AddRangeAsync(stored);
        await DataContext.SaveChangesAsync();

        var first = Win();
        var second = Win();
        var result = await Submit(UserId, first, second);

        result.Value.Accepted.Should().Equal(first.Id);
        result.Value.Rejected.Should().Equal(second.Id);
    }

    [Fact]
    public async Task Execute_NullResults_IsBadRequest_NotAnException()
    {
        var result = await Mocker.CreateInstance<SubmitMiniGameResultsCommandHandler>()
            .ExecuteAsync(UserId, new SubmitMiniGameResultsCommand { Results = null! });

        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task Execute_NullItem_IsBadRequest()
    {
        var result = await Mocker.CreateInstance<SubmitMiniGameResultsCommandHandler>()
            .ExecuteAsync(UserId, new SubmitMiniGameResultsCommand { Results = [Win(), null!] });

        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task Execute_IdOwnedByAnotherUser_IsRejected_AndNotOverwritten()
    {
        var item = Win();
        await Submit(UserId, item);

        var result = await Submit(Guid.NewGuid(), item with { Score = 50 });

        result.Value.Rejected.Should().Equal(item.Id);
        DataContext.ChangeTracker.Clear();
        var row = await DataContext.MiniGameResults.SingleAsync();
        row.UserId.Should().Be(UserId);
        row.Score.Should().Be(21);
    }

    [Fact]
    public async Task Execute_EmptyBatch_IsBadRequest()
    {
        var result = await Submit(UserId);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task Execute_OversizedBatch_IsBadRequest()
    {
        var items = Enumerable.Range(0, SubmitMiniGameResultsCommandValidator.MaxBatchSize + 1)
            .Select(_ => Win())
            .ToArray();

        var result = await Submit(UserId, items);

        result.Status.Should().Be(ResultStatus.BadRequest);
        (await DataContext.MiniGameResults.AnyAsync()).Should().BeFalse();
    }
}
