using FluentValidation;

namespace SportsData.Producer.Application.Documents.Commands.ReprocessDeadLetterQueue;

public class ReprocessDeadLetterQueueCommandValidator : AbstractValidator<ReprocessDeadLetterQueueCommand>
{
    /// <summary>
    /// Maximum number of DLQ messages that can be reprocessed in a single request.
    /// Prevents runaway bulk operations from blocking the bus for too long.
    /// </summary>
    public const int MaxReprocessCount = 5_000;

    public ReprocessDeadLetterQueueCommandValidator()
    {
        RuleFor(x => x.Count)
            .GreaterThan(0)
                .WithMessage("Count must be greater than zero.")
            .LessThanOrEqualTo(MaxReprocessCount)
                .WithMessage($"Count cannot exceed {MaxReprocessCount} per request.");
    }
}
