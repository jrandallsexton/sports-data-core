using FluentAssertions;
using FluentValidation;

using Moq;

using SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Devices;

public class UnregisterDeviceCommandHandlerTests : ApiTestBase<UnregisterDeviceCommandHandler>
{
    public UnregisterDeviceCommandHandlerTests()
    {
        Mocker.Use<IValidator<UnregisterDeviceCommand>>(new UnregisterDeviceCommandValidator());
    }

    [Fact]
    public async Task ExecuteAsync_PublishesUserDeviceUnregistered_WithJwtUser_AndTrimmedInstallationId()
    {
        var userId = Guid.NewGuid();
        var handler = Mocker.CreateInstance<UnregisterDeviceCommandHandler>();
        var command = new UnregisterDeviceCommand { UserId = userId, InstallationId = "  install-A  " };

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.Is<UserDeviceUnregistered>(e => e.UserId == userId && e.InstallationId == "install-A"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsBadRequest_AndDoesNotPublish_WhenInstallationIdBlank()
    {
        var handler = Mocker.CreateInstance<UnregisterDeviceCommandHandler>();
        var command = new UnregisterDeviceCommand { UserId = Guid.NewGuid(), InstallationId = "" };

        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        Mocker.GetMock<IEventBus>().Verify(x => x.Publish(
            It.IsAny<UserDeviceUnregistered>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
