using FluentValidation;

using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.MiniGames.Commands.SubmitMiniGameResults;

/// <summary>
/// Batch-level rules only. A bad item doesn't fail the batch -- the handler
/// runs <see cref="MiniGameResultSubmissionValidator"/> per item and reports
/// it as rejected, so one malformed result can't block the rest of the queue.
/// </summary>
public class SubmitMiniGameResultsCommandValidator : AbstractValidator<SubmitMiniGameResultsCommand>
{
    public const int MaxBatchSize = 100;

    public SubmitMiniGameResultsCommandValidator()
    {
        RuleFor(x => x.Results)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(r => r.Count is > 0 and <= MaxBatchSize)
            .WithMessage($"Submit between 1 and {MaxBatchSize} results.");

        // A null element is a malformed body, not one bad result: fail the batch.
        RuleForEach(x => x.Results).NotNull().When(x => x.Results is not null);
    }
}

public class MiniGameResultSubmissionValidator : AbstractValidator<MiniGameResultSubmission>
{
    public const int MaxScore = 999;
    public const int MaxDurationSeconds = 24 * 60 * 60;
    public const int MaxDetailLength = 4096;

    // No future-PlayedUtc rule: a phone whose clock runs ahead would have every
    // game rejected, and the client drops rejected results, so the games would
    // be lost. The handler clamps PlayedUtc to the server's clock instead.
    public MiniGameResultSubmissionValidator()
    {
        RuleFor(x => x.Id).NotEmpty();

        RuleFor(x => x.GameKey)
            .Must(k => k is not null && MiniGameKeys.Known.Contains(k))
            .WithMessage("Unknown game.");

        RuleFor(x => x.Outcome).IsInEnum();
        RuleFor(x => x.Score).InclusiveBetween(0, MaxScore);
        RuleFor(x => x.OpponentScore).InclusiveBetween(0, MaxScore);
        RuleFor(x => x.DurationSeconds).InclusiveBetween(0, MaxDurationSeconds);

        // Every known game decides its outcome on the scores (see MiniGameKeys).
        RuleFor(x => x)
            .Must(x => x.Outcome == ExpectedOutcome(x.Score, x.OpponentScore))
            .WithName(nameof(MiniGameResultSubmission.Outcome))
            .WithMessage("Outcome doesn't match the scores.");

        RuleFor(x => x.GameVersion)
            .NotEmpty()
            .MaximumLength(MiniGameResult.GameVersionMaxLength);

        RuleFor(x => x.Seed).MaximumLength(MiniGameResult.SeedMaxLength);

        RuleFor(x => x.Detail)
            .Must(d => d!.Value.GetRawText().Length <= MaxDetailLength)
            .When(x => x.Detail.HasValue)
            .WithMessage($"Detail exceeds {MaxDetailLength} characters.");

        RuleFor(x => x.PlayedUtc).NotEmpty();
    }

    private static MiniGameOutcome ExpectedOutcome(int score, int opponentScore) =>
        score > opponentScore ? MiniGameOutcome.Win
        : score < opponentScore ? MiniGameOutcome.Loss
        : MiniGameOutcome.Tie;
}
