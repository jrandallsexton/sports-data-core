using FluentValidation;

using SportsData.Api.Application.Admin.Jobs;
using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin.Commands.GenerateLoadTest;

public interface IGenerateLoadTestCommandHandler
{
    Task<Result<GenerateLoadTestResult>> ExecuteAsync(GenerateLoadTestCommand command, CancellationToken cancellationToken);
}

public class GenerateLoadTestCommandHandler : IGenerateLoadTestCommandHandler
{
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly ILogger<GenerateLoadTestCommandHandler> _logger;

    public GenerateLoadTestCommandHandler(
        IProvideBackgroundJobs backgroundJobProvider,
        ILogger<GenerateLoadTestCommandHandler> logger)
    {
        _backgroundJobProvider = backgroundJobProvider;
        _logger = logger;
    }

    public async Task<Result<GenerateLoadTestResult>> ExecuteAsync(
        GenerateLoadTestCommand command,
        CancellationToken cancellationToken)
    {
        // Validate command
        var validator = new GenerateLoadTestCommandValidator();
        var validationResult = await validator.ValidateAsync(command, cancellationToken);

        if (!validationResult.IsValid)
        {
            return new Failure<GenerateLoadTestResult>(
                default!,
                ResultStatus.BadRequest,
                validationResult.Errors.ToList());
        }

        var testId = Guid.NewGuid();
        var publishedUtc = DateTime.UtcNow;

        _logger.LogInformation(
            "[KEDA-Test] Enqueueing background job to publish load test events. TestId={TestId}, Count={Count}, Target={Target}, BatchSize={BatchSize}",
            testId, command.Count, command.Target, command.BatchSize);

        // Enqueue background job to publish events asynchronously
        _backgroundJobProvider.Enqueue<IPublishLoadTestEventsJob>(job =>
            job.ExecuteAsync(testId, command.Count, command.Target, command.BatchSize, publishedUtc));

        var totalEvents = command.Target == LoadTestTarget.Both ? command.Count * 2 : command.Count;
        var totalBatches = (int)Math.Ceiling((double)command.Count / command.BatchSize);

        var result = new GenerateLoadTestResult
        {
            TestId = testId,
            EventsPublished = totalEvents,
            Target = command.Target,
            Batches = totalBatches,
            BatchSize = command.BatchSize,
            PublishedUtc = publishedUtc,
            Message = $"Load test job enqueued. Use TestId={testId} to track progress in logs. {totalEvents} total events will be published across {totalBatches} batches."
        };

        _logger.LogInformation(
            "[KEDA-Test] Load test job enqueued successfully. TestId={TestId}, TotalEvents={TotalEvents}, Batches={Batches}",
            testId, totalEvents, totalBatches);

        return new Success<GenerateLoadTestResult>(result);
    }
}

public class GenerateLoadTestCommandValidator : AbstractValidator<GenerateLoadTestCommand>
{
    public GenerateLoadTestCommandValidator()
    {
        RuleFor(x => x.Count)
            .InclusiveBetween(1, 1000)
            .WithMessage("Count must be between 1 and 1000");

        RuleFor(x => x.Target)
            .IsInEnum()
            .WithMessage("Target must be Producer, Provider, or Both");

        RuleFor(x => x.BatchSize)
            .InclusiveBetween(1, 100)
            .WithMessage("BatchSize must be between 1 and 100");
    }
}
