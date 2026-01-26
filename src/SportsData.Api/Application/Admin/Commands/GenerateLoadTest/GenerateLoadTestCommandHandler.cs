using FluentValidation;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;

namespace SportsData.Api.Application.Admin.Commands.GenerateLoadTest;

public interface IGenerateLoadTestCommandHandler
{
    Task<Result<GenerateLoadTestResult>> ExecuteAsync(GenerateLoadTestCommand command, CancellationToken cancellationToken);
}

public class GenerateLoadTestCommandHandler : IGenerateLoadTestCommandHandler
{
    private readonly IEventBus _eventBus;
    private readonly ILogger<GenerateLoadTestCommandHandler> _logger;

    public GenerateLoadTestCommandHandler(
        IEventBus eventBus,
        ILogger<GenerateLoadTestCommandHandler> logger)
    {
        _eventBus = eventBus;
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
        var normalizedTarget = command.Target.ToLowerInvariant();

        _logger.LogInformation(
            "[KEDA-Test] Starting load test. TestId={TestId}, Count={Count}, Target={Target}, BatchSize={BatchSize}",
            testId, command.Count, normalizedTarget, command.BatchSize);

        var totalBatches = (int)Math.Ceiling((double)command.Count / command.BatchSize);
        var publishTasks = new List<Task>();

        for (int batch = 0; batch < totalBatches; batch++)
        {
            var startIdx = batch * command.BatchSize;
            var endIdx = Math.Min(startIdx + command.BatchSize, command.Count);
            var batchNumber = batch + 1;

            for (int i = startIdx; i < endIdx; i++)
            {
                var jobNumber = i + 1;

                if (normalizedTarget == "producer" || normalizedTarget == "both")
                {
                    var producerEvent = new LoadTestProducerEvent(
                        TestId: testId,
                        BatchNumber: batchNumber,
                        JobNumber: jobNumber,
                        PublishedUtc: publishedUtc
                    );
                    publishTasks.Add(_eventBus.Publish(producerEvent, cancellationToken));
                }

                if (normalizedTarget == "provider" || normalizedTarget == "both")
                {
                    var providerEvent = new LoadTestProviderEvent(
                        TestId: testId,
                        BatchNumber: batchNumber,
                        JobNumber: jobNumber,
                        PublishedUtc: publishedUtc
                    );
                    publishTasks.Add(_eventBus.Publish(providerEvent, cancellationToken));
                }
            }
        }

        await Task.WhenAll(publishTasks);

        var actualJobCount = normalizedTarget == "both" ? command.Count * 2 : command.Count;

        _logger.LogInformation(
            "[KEDA-Test] Load test published. TestId={TestId}, EventsPublished={EventsPublished}, Target={Target}",
            testId, actualJobCount, normalizedTarget);

        var result = new GenerateLoadTestResult
        {
            TestId = testId,
            EventsPublished = actualJobCount,
            Target = normalizedTarget,
            Batches = totalBatches,
            BatchSize = command.BatchSize,
            PublishedUtc = publishedUtc,
            Message = $"Published {actualJobCount} events to RabbitMQ. Monitor KEDA ScaledObjects and HPAs for autoscaling."
        };

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
            .Must(target => new[] { "producer", "provider", "both" }.Contains(target.ToLowerInvariant()))
            .WithMessage("Target must be 'producer', 'provider', or 'both'");

        RuleFor(x => x.BatchSize)
            .InclusiveBetween(1, 100)
            .WithMessage("BatchSize must be between 1 and 100");
    }
}
