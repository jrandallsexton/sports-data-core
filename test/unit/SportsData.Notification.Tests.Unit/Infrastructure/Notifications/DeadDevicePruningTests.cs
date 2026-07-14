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

    private static AppDataContext NewContext(string? dbName = null) =>
        new(new DbContextOptionsBuilder<AppDataContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString()[..8])
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
    public async Task MarkDeadDeviceForRemovalAsync_NotFound_DeletesRow()
    {
        // Seed in one context and dispose it, then operate on a fresh context over
        // the same in-memory DB — so the device is NOT tracked, exercising the
        // untracked-stub delete path the AsNoTracking send loops actually hit.
        var dbName = Guid.NewGuid().ToString()[..8];
        Guid deviceId;
        await using (var seedCtx = NewContext(dbName))
        {
            deviceId = await SeedDeviceAsync(seedCtx);
        }

        await using var ctx = NewContext(dbName);
        var pruned = await ctx.MarkDeadDeviceForRemovalAsync(
            new Failure<string>(string.Empty, ResultStatus.NotFound, new List<ValidationFailure>()),
            deviceId,
            NullLogger.Instance);

        pruned.Should().BeTrue();
        (await ctx.UserDevices.FindAsync(deviceId)).Should().BeNull();
    }

    [Fact]
    public async Task MarkDeadDeviceForRemovalAsync_TransientError_KeepsRow()
    {
        await using var ctx = NewContext();
        var deviceId = await SeedDeviceAsync(ctx);

        var pruned = await ctx.MarkDeadDeviceForRemovalAsync(
            new Failure<string>(string.Empty, ResultStatus.Error, new List<ValidationFailure>()),
            deviceId,
            NullLogger.Instance);

        pruned.Should().BeFalse();
        (await ctx.UserDevices.FindAsync(deviceId)).Should().NotBeNull();
    }

    [Fact]
    public async Task MarkDeadDeviceForRemovalAsync_Success_KeepsRow()
    {
        await using var ctx = NewContext();
        var deviceId = await SeedDeviceAsync(ctx);

        var pruned = await ctx.MarkDeadDeviceForRemovalAsync(
            new Success<string>("msg-id"), deviceId, NullLogger.Instance);

        pruned.Should().BeFalse();
        (await ctx.UserDevices.FindAsync(deviceId)).Should().NotBeNull();
    }

    [Fact]
    public async Task MarkDeadDeviceForRemovalAsync_MissingRow_IsNonFatal()
    {
        // The row was already deleted (concurrent prune / unregister). A
        // best-effort prune must not throw / fail the message.
        await using var ctx = NewContext();

        var act = () => ctx.MarkDeadDeviceForRemovalAsync(
            new Failure<string>(string.Empty, ResultStatus.NotFound, new List<ValidationFailure>()),
            Guid.NewGuid(),
            NullLogger.Instance);

        await act.Should().NotThrowAsync();
    }
}
