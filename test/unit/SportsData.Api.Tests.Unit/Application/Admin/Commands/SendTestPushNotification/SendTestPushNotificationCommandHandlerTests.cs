using FluentAssertions;
using FluentValidation.Results;

using Moq;

using SportsData.Api.Application.Admin.Commands.SendTestPushNotification;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.SendTestPushNotification;

public class SendTestPushNotificationCommandHandlerTests : ApiTestBase<SendTestPushNotificationCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnBadRequest_WhenTokenIsEmpty()
    {
        var handler = Mocker.CreateInstance<SendTestPushNotificationCommandHandler>();
        var command = new SendTestPushNotificationCommand { Token = "  " };

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);

        Mocker.GetMock<IPushNotificationSender>()
            .Verify(x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
                Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnSuccess_WithFcmMessageIdAndTimestamp_WhenSenderSucceeds()
    {
        var fcmMessageId = "projects/sportdeets/messages/abc123";
        var fixedNow = new DateTime(2026, 6, 2, 12, 0, 0, DateTimeKind.Utc);

        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(fixedNow);

        Mocker.GetMock<IPushNotificationSender>()
            .Setup(x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>(fcmMessageId));

        var handler = Mocker.CreateInstance<SendTestPushNotificationCommandHandler>();
        var command = new SendTestPushNotificationCommand
        {
            Token = "fcm-token-xyz",
            Title = "Custom title",
            Body = "Custom body",
        };

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.MessageId.Should().Be(fcmMessageId);
        result.Value.SentUtc.Should().Be(fixedNow);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldFallBackToDefaultTitleAndBody_WhenCommandFieldsAreBlank()
    {
        Mocker.GetMock<IPushNotificationSender>()
            .Setup(x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("messages/id-1"));

        var handler = Mocker.CreateInstance<SendTestPushNotificationCommandHandler>();
        var command = new SendTestPushNotificationCommand
        {
            Token = "fcm-token-xyz",
            Title = null,
            Body = "   "
        };

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        Mocker.GetMock<IPushNotificationSender>()
            .Verify(x => x.SendAsync(
                "fcm-token-xyz",
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
                It.Is<string>(s => !string.IsNullOrWhiteSpace(s)),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()),
                Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPropagateSenderFailure_WhenSenderReturnsFailure()
    {
        var senderError = new ValidationFailure("token", "FCM Unregistered: token expired");
        Mocker.GetMock<IPushNotificationSender>()
            .Setup(x => x.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<string>(string.Empty, ResultStatus.Error, [senderError]));

        var handler = Mocker.CreateInstance<SendTestPushNotificationCommandHandler>();
        var command = new SendTestPushNotificationCommand { Token = "fcm-token-xyz" };

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var failure = (Failure<SendTestPushNotificationResponse>)result;
        failure.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("Unregistered");
    }
}
