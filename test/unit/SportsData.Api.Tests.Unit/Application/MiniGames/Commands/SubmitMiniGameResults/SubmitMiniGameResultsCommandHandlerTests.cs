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
        var clock = Mocker.GetMock<IDateTimeProvider>();
        clock.Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IValidator<SubmitMiniGameResultsCommand>>(new SubmitMiniGameResultsCommandValidator());
        Mocker.Use<IValidator<MiniGameResultSubmission>>(new MiniGameResultSubmissionValidator(clock.Object));
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
    public async Task Execute_SameIdTwiceInOneBatch_FirstWins()
    {
        var item = Win();

        var result = await Submit(UserId, item, item with { Score = 35 });

        result.Value.Accepted.Should().Equal(item.Id);
        result.Value.Duplicate.Should().Equal(item.Id);
        DataContext.ChangeTracker.Clear();
        (await DataContext.MiniGameResults.SingleAsync()).Score.Should().Be(21);
    }

    [Fact]
    public async Task Execute_InvalidItem_IsRejected_WithoutBlockingTheRest()
    {
        var good = Win();
        var unknownGame = Win() with { GameKey = "tic-tac-toe" };
        var wrongOutcome = Win() with { Outcome = MiniGameOutcome.Loss };
        var future = Win() with { PlayedUtc = FixedNow.AddHours(1) };
        var negative = Win() with { Score = -1 };

        var result = await Submit(UserId, good, unknownGame, wrongOutcome, future, negative);

        result.IsSuccess.Should().BeTrue();
        result.Value.Accepted.Should().Equal(good.Id);
        result.Value.Rejected.Should().BeEquivalentTo(
            [unknownGame.Id, wrongOutcome.Id, future.Id, negative.Id]);
        (await DataContext.MiniGameResults.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Execute_SmallClockSkew_IsAccepted()
    {
        var item = Win() with { PlayedUtc = FixedNow.AddMinutes(2) };

        var result = await Submit(UserId, item);

        result.Value.Accepted.Should().Equal(item.Id);
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
