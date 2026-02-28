using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using FluentValidation;
using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Config;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Documents.Commands.ReprocessDeadLetterQueue;

public interface IReprocessDeadLetterQueueCommandHandler
{
    Task<Result<ReprocessDeadLetterQueueResult>> ExecuteAsync(
        ReprocessDeadLetterQueueCommand command,
        CancellationToken cancellationToken = default);
}

/// <param name="Requested">Number of messages requested from the DLQ.</param>
/// <param name="Requeued">Number of messages successfully re-published.</param>
/// <param name="Errors">Per-message error descriptions for any that could not be requeued.</param>
public record ReprocessDeadLetterQueueResult(
    int Requested,
    int Requeued,
    IReadOnlyList<string> Errors);

public class ReprocessDeadLetterQueueCommandHandler : IReprocessDeadLetterQueueCommandHandler
{
    private readonly ILogger<ReprocessDeadLetterQueueCommandHandler> _logger;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IValidator<ReprocessDeadLetterQueueCommand> _validator;

    /// Default queue name used when no override is provided in config or the command.
    private const string DefaultQueueName = "document-dead-letter";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = true
    };

    public ReprocessDeadLetterQueueCommandHandler(
        ILogger<ReprocessDeadLetterQueueCommandHandler> logger,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        IValidator<ReprocessDeadLetterQueueCommand> validator)
    {
        _logger = logger;
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _validator = validator;
    }

    public async Task<Result<ReprocessDeadLetterQueueResult>> ExecuteAsync(
        ReprocessDeadLetterQueueCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
            return new Failure<ReprocessDeadLetterQueueResult>(
                new ReprocessDeadLetterQueueResult(command.Count, 0, []),
                ResultStatus.Validation,
                validation.Errors);

        var queueName = command.QueueName
            ?? _configuration["SportsData.Producer:DeadLetterQueue:QueueName"]
            ?? DefaultQueueName;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["QueueName"] = queueName,
            ["RequestedCount"] = command.Count
        }))
        {
            _logger.LogInformation(
                "DLQ_REPROCESS: Beginning. QueueName={QueueName}, Count={Count}", queueName, command.Count);

            var managementApiBaseUrl = _configuration[CommonConfigKeys.RabbitMqManagementApiBaseUrl];
            var username = _configuration[CommonConfigKeys.RabbitMqUsername];
            var password = _configuration[CommonConfigKeys.RabbitMqPassword];

            if (string.IsNullOrWhiteSpace(managementApiBaseUrl))
                return new Failure<ReprocessDeadLetterQueueResult>(
                    new ReprocessDeadLetterQueueResult(command.Count, 0, []),
                    ResultStatus.Error,
                    [new ValidationFailure(
                        "ManagementApiBaseUrl",
                        $"'{CommonConfigKeys.RabbitMqManagementApiBaseUrl}' is not configured.")]);

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                return new Failure<ReprocessDeadLetterQueueResult>(
                    new ReprocessDeadLetterQueueResult(command.Count, 0, []),
                    ResultStatus.Error,
                    [new ValidationFailure(
                        "ManagementApiCredentials",
                        $"'{CommonConfigKeys.RabbitMqUsername}' and '{CommonConfigKeys.RabbitMqPassword}' must be configured.")]);

            try
            {
                var payloads = await FetchMessagesAsync(
                    managementApiBaseUrl, username!, password!, queueName, command.Count, cancellationToken);

                _logger.LogInformation(
                    "DLQ_REPROCESS: Fetched {Fetched} message(s) from queue.", payloads.Count);

                var errors = new List<string>();
                var requeued = 0;

                // Use direct publishing to bypass outbox (no DbContext required for DLQ reprocessing)
                using (_deliveryScope.Use(DeliveryMode.Direct))
                {
                    foreach (var (payload, index) in payloads.Select((p, i) => (p, i)))
                    {
                        try
                        {
                            var document = ExtractDocumentCreated(payload);

                            if (document is null)
                            {
                                var error = $"Message {index}: unable to deserialize DocumentCreated from payload.";
                                _logger.LogWarning("DLQ_REPROCESS: {Error}", error);
                                errors.Add(error);
                                continue;
                            }

                            await _eventBus.Publish(document, cancellationToken);
                            requeued++;

                            _logger.LogInformation(
                                "DLQ_REPROCESS: Re-published message {Index}. DocumentId={DocumentId}, DocumentType={DocumentType}, AttemptCount={AttemptCount}",
                                index, document.Id, document.DocumentType, document.AttemptCount);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            throw;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "DLQ_REPROCESS: Failed to re-publish message {Index}.", index);
                            errors.Add($"Message {index}: publish failed. See logs for details.");
                        }
                    }
                }

                _logger.LogInformation(
                    "DLQ_REPROCESS: Complete. Requeued={Requeued}/{Total}, Errors={ErrorCount}",
                    requeued, payloads.Count, errors.Count);

                return new Success<ReprocessDeadLetterQueueResult>(
                    new ReprocessDeadLetterQueueResult(command.Count, requeued, errors));
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "DLQ_REPROCESS: RabbitMQ Management API request failed.");
                return new Failure<ReprocessDeadLetterQueueResult>(
                    new ReprocessDeadLetterQueueResult(command.Count, 0, []),
                    ResultStatus.Error,
                    [new ValidationFailure("Exception", "DLQ reprocess failed. See logs for details.")]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DLQ_REPROCESS: Unexpected error.");
                return new Failure<ReprocessDeadLetterQueueResult>(
                    new ReprocessDeadLetterQueueResult(command.Count, 0, []),
                    ResultStatus.Error,
                    [new ValidationFailure("Exception", "DLQ reprocess failed. See logs for details.")]);
            }
        }
    }

    // ---------------------------------------------------------------------------
    // RabbitMQ Management API
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Calls the RabbitMQ Management HTTP API to pull <paramref name="count"/>
    /// messages from the given queue using <c>ack_requeue_false</c>, removing them
    /// from the DLQ on fetch. Messages that fail reprocessing will be re-dead-lettered
    /// naturally by <c>DocumentCreatedHandler</c>.
    /// </summary>
    private async Task<List<string>> FetchMessagesAsync(
        string managementApiBaseUrl,
        string username,
        string password,
        string queueName,
        int count,
        CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("RabbitMqManagement");

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        // POST /api/queues/%2F/{queue}/get
        // ackmode=ack_requeue_false: messages are removed from the DLQ on fetch.
        // This is safe because DocumentCreatedHandler will re-dead-letter any message
        // that fails processing again, so no permanent loss can occur.
        var body = JsonSerializer.Serialize(new
        {
            count,
            ackmode = "ack_requeue_false",
            encoding = "auto",
            truncate = 50000
        });

        var encodedQueue = Uri.EscapeDataString(queueName);
        var url = $"{managementApiBaseUrl.TrimEnd('/')}/api/queues/%2F/{encodedQueue}/get";

        var response = await client.PostAsync(
            url,
            new StringContent(body, Encoding.UTF8, "application/json"),
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var items = JsonSerializer.Deserialize<List<RabbitMqManagementMessage>>(json, _jsonOptions);

        return items?
            .Where(m => m.PayloadEncoding == "string" && !string.IsNullOrWhiteSpace(m.Payload))
            .Select(m => m.Payload!)
            .ToList() ?? [];
    }

    /// <summary>
    /// Extracts the inner <see cref="DocumentCreated"/> message from a MassTransit
    /// JSON envelope and sets <c>AttemptCount</c> to <c>MaxAttempts - 1</c>,
    /// giving the message exactly one final attempt before hitting the DLQ threshold.
    /// </summary>
    private static DocumentCreated? ExtractDocumentCreated(string envelopeJson)
    {
        using var doc = JsonDocument.Parse(envelopeJson);
        var root = doc.RootElement;

        if (!root.TryGetProperty("message", out var messageElement))
        {
            // Try top-level (plain message, not wrapped in an envelope)
            messageElement = root;
        }

        var document = JsonSerializer.Deserialize<DocumentCreated>(
            messageElement.GetRawText(),
            _jsonOptions);

        if (document is null)
            return null;

        // Always set to (MaxAttempts - 1): gives exactly ONE final attempt before hitting the DLQ threshold again
        return document with { AttemptCount = DocumentProcessingConstants.MaxAttempts - 1 };
    }

    // ---------------------------------------------------------------------------
    // Private DTOs for Management API response deserialization
    // ---------------------------------------------------------------------------

    private record RabbitMqManagementMessage(
        [property: JsonPropertyName("payload")] string? Payload,
        [property: JsonPropertyName("payload_encoding")] string? PayloadEncoding,
        [property: JsonPropertyName("message_count")] int MessageCount
    );
}
