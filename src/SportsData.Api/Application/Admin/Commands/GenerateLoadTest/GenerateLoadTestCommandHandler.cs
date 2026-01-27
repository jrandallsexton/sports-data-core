using FluentValidation;

using SportsData.Api.Infrastructure.Data;
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
    private readonly AppDataContext _dbContext;

    public GenerateLoadTestCommandHandler(
        IEventBus eventBus,
        ILogger<GenerateLoadTestCommandHandler> logger,
        AppDataContext dbContext)
    {
        _eventBus = eventBus;
        _logger = logger;
        _dbContext = dbContext;
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
            "[KEDA-Test] Starting load test. TestId={TestId}, Count={Count}, Target={Target}, BatchSize={BatchSize}",
            testId, command.Count, command.Target, command.BatchSize);

        var totalBatches = (int)Math.Ceiling((double)command.Count / command.BatchSize);

        try
        {
            for (int batch = 0; batch < totalBatches; batch++)
            {
                var startIdx = batch * command.BatchSize;
                var endIdx = Math.Min(startIdx + command.BatchSize, command.Count);
                var batchNumber = batch + 1;
                var publishTasks = new List<Task>();

                for (int i = startIdx; i < endIdx; i++)
                {
                    var jobNumber = i + 1;

                    if (command.Target == LoadTestTarget.Producer || command.Target == LoadTestTarget.Both)
                    {
                        var producerEvent = new LoadTestProducerEvent(
                            TestId: testId,
                            BatchNumber: batchNumber,
                            JobNumber: jobNumber,
                            PublishedUtc: publishedUtc
                        );
                        publishTasks.Add(_eventBus.Publish(producerEvent, cancellationToken));
                    }

                    if (command.Target == LoadTestTarget.Provider || command.Target == LoadTestTarget.Both)
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

                // Await batch completion before moving to next batch to limit concurrency
                await Task.WhenAll(publishTasks);
            }

            // Trigger MassTransit outbox to flush queued messages
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[KEDA-Test] Failed to publish load test events. TestId={TestId}", testId);
            return new Failure<GenerateLoadTestResult>(
                default!,
                ResultStatus.Error,
                new List<FluentValidation.Results.ValidationFailure>
                {
                    new("EventBus", $"Failed to publish events to RabbitMQ: {ex.Message}")
                });
        }

        var actualJobCount = command.Target == LoadTestTarget.Both ? command.Count * 2 : command.Count;

        _logger.LogInformation(
            "[KEDA-Test] Load test published. TestId={TestId}, EventsPublished={EventsPublished}, Target={Target}",
            testId, actualJobCount, command.Target);

        var result = new GenerateLoadTestResult
        {
            TestId = testId,
            EventsPublished = actualJobCount,
            Target = command.Target,
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
            .IsInEnum()
            .WithMessage("Target must be Producer, Provider, or Both");

        RuleFor(x => x.BatchSize)
            .InclusiveBetween(1, 100)
            .WithMessage("BatchSize must be between 1 and 100");
    }
}
