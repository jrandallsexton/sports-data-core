using FluentAssertions;

using SportsData.Api.Application.UI.Devices.Commands.RegisterDevice;
using SportsData.Api.Application.UI.Devices.Commands.UnregisterDevice;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Devices;

public class DeviceCommandValidatorsTests
{
    private static RegisterDeviceCommand ValidRegister() => new()
    {
        UserId = Guid.NewGuid(),
        InstallationId = "install-A",
        FcmToken = "tok",
        Platform = "ios"
    };

    [Fact]
    public void Register_Accepts_ValidCommand()
    {
        var result = new RegisterDeviceCommandValidator().Validate(ValidRegister());
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_Rejects_BlankInstallationId(string installationId)
    {
        var cmd = ValidRegister();
        cmd.InstallationId = installationId;

        var result = new RegisterDeviceCommandValidator().Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.InstallationId));
    }

    [Fact]
    public void Register_Rejects_OverlongInstallationId()
    {
        var cmd = ValidRegister();
        cmd.InstallationId = new string('a', 129);

        var result = new RegisterDeviceCommandValidator().Validate(cmd);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterDeviceCommand.InstallationId));
    }

    [Fact]
    public void Unregister_Accepts_ValidCommand()
    {
        var result = new UnregisterDeviceCommandValidator()
            .Validate(new UnregisterDeviceCommand { UserId = Guid.NewGuid(), InstallationId = "install-A" });

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Unregister_Rejects_BlankInstallationId(string installationId)
    {
        var result = new UnregisterDeviceCommandValidator()
            .Validate(new UnregisterDeviceCommand { UserId = Guid.NewGuid(), InstallationId = installationId });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UnregisterDeviceCommand.InstallationId));
    }

    [Fact]
    public void Unregister_Rejects_OverlongInstallationId()
    {
        var result = new UnregisterDeviceCommandValidator()
            .Validate(new UnregisterDeviceCommand { UserId = Guid.NewGuid(), InstallationId = new string('a', 129) });

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(UnregisterDeviceCommand.InstallationId));
    }
}
