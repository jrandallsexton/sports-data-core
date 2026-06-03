using FluentValidation.Results;

using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Commands.SendTestPushNotification;

public interface ISendTestPushNotificationCommandHandler
{
    Task<Result<SendTestPushNotificationResponse>> ExecuteAsync(
        SendTestPushNotificationCommand command,
        CancellationToken cancellationToken = default);
}

public class SendTestPushNotificationCommandHandler : ISendTestPushNotificationCommandHandler
{
    private const string DefaultTitle = "sportDeets test push";
    private const string DefaultBody = "If you can read this, the pipeline works.";

    private readonly IPushNotificationSender _sender;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<SendTestPushNotificationCommandHandler> _logger;

    public SendTestPushNotificationCommandHandler(
        IPushNotificationSender sender,
        IDateTimeProvider dateTimeProvider,
        ILogger<SendTestPushNotificationCommandHandler> logger)
    {
        _sender = sender;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result<SendTestPushNotificationResponse>> ExecuteAsync(
        SendTestPushNotificationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            return new Failure<SendTestPushNotificationResponse>(
                SendTestPushNotificationResponse.Empty(),
                ResultStatus.BadRequest,
                [new ValidationFailure(nameof(command.Token), "Token is required")]);
        }

        var title = string.IsNullOrWhiteSpace(command.Title) ? DefaultTitle : command.Title;
        var body = string.IsNullOrWhiteSpace(command.Body) ? DefaultBody : command.Body;

        _logger.LogInformation(
            "Dispatching test push. Title={Title}, HasData={HasData}",
            title,
            command.Data is { Count: > 0 });

        var sentUtc = _dateTimeProvider.UtcNow();
        var sendResult = await _sender.SendAsync(
            command.Token,
            title,
            body,
            command.Data,
            cancellationToken);

        if (sendResult is not Success<string> success)
        {
            return new Failure<SendTestPushNotificationResponse>(
                SendTestPushNotificationResponse.Empty(),
                sendResult.Status,
                sendResult is Failure<string> f ? f.Errors : []);
        }

        return new Success<SendTestPushNotificationResponse>(new SendTestPushNotificationResponse
        {
            MessageId = success.Value,
            SentUtc = sentUtc
        });
    }
}
