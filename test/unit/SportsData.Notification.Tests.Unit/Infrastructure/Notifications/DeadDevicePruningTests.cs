using FluentAssertions;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SportsData.Core.Common;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Infrastructure.Notifications;

public class DeadDevicePruningTests
{
    private static readonly DateTime FixedNow = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    private static AppDataContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()[..8])
            .Options);

    private static async Task<Guid> SeedDeviceAsync(AppDataContext ctx)
    {
        var id = Guid.NewGuid();
        ctx.UserDevices.Add(new UserDevice
        {
            Id = id,
            UserId = Guid.NewGuid(),
            InstallationId = Guid.NewGuid().ToString(),
            FcmToken = "tok",
            Platform = "ios",
            NotificationsEnabled = true,
            LastSeenUtc = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid(),
        });
        await ctx.SaveChangesAsync();
        return id;
    }

    [Fact]
    public void IsDeadTokenFailure_TrueOnlyForNotFoundFailure()
    {
        DeadDevicePruning.IsDeadTokenFailure(
            new Failure<string>(string.Empty, ResultStatus.NotFound, new List<ValidationFailure>()))
            .Should().BeTrue();

        DeadDevicePruning.IsDeadTokenFailure(
            new Failure<string>(string.Empty, ResultStatus.Error, new List<ValidationFailure>()))
            .Should().BeFalse();

        DeadDevicePruning.IsDeadTokenFailure(new Success<string>("msg-id"))
            .Should().BeFalse();
    }

    [Fact]
    public async Task MarkDeadDeviceForRemoval_NotFound_DeletesRowOnSave()
    {
        await using var ctx = NewContext();
        var deviceId = await SeedDeviceAsync(ctx);

        var marked = ctx.MarkDeadDeviceForRemoval(
            new Failure<string>(string.Empty, ResultStatus.NotFound, new List<ValidationFailure>()),
            deviceId,
            NullLogger.Instance);
        await ctx.SaveChangesAsync();

        marked.Should().BeTrue();
        (await ctx.UserDevices.FindAsync(deviceId)).Should().BeNull();
    }

    [Fact]
    public async Task MarkDeadDeviceForRemoval_TransientError_KeepsRow()
    {
        await using var ctx = NewContext();
        var deviceId = await SeedDeviceAsync(ctx);

        var marked = ctx.MarkDeadDeviceForRemoval(
            new Failure<string>(string.Empty, ResultStatus.Error, new List<ValidationFailure>()),
            deviceId,
            NullLogger.Instance);
        await ctx.SaveChangesAsync();

        marked.Should().BeFalse();
        (await ctx.UserDevices.FindAsync(deviceId)).Should().NotBeNull();
    }

    [Fact]
    public async Task MarkDeadDeviceForRemoval_Success_KeepsRow()
    {
        await using var ctx = NewContext();
        var deviceId = await SeedDeviceAsync(ctx);

        var marked = ctx.MarkDeadDeviceForRemoval(new Success<string>("msg-id"), deviceId, NullLogger.Instance);
        await ctx.SaveChangesAsync();

        marked.Should().BeFalse();
        (await ctx.UserDevices.FindAsync(deviceId)).Should().NotBeNull();
    }
}
