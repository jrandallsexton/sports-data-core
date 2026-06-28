using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class ContestOddsUpdatedConsumerTests : NotificationTestBase<ContestOddsUpdatedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public ContestOddsUpdatedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));
    }

    private static ConsumeContext<ContestOddsUpdated> ContextFor(ContestOddsUpdated msg)
    {
        var ctx = new Mock<ConsumeContext<ContestOddsUpdated>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static ContestOddsUpdated Msg(
        Guid contestId,
        decimal? oldSpread = null, decimal? newSpread = null,
        decimal? oldTotal = null, decimal? newTotal = null)
        => new(contestId, "odds updated", "1", "DraftKings",
            oldSpread, newSpread, oldTotal, newTotal,
            null, Sport.FootballNcaa, 2026, Guid.NewGuid(), Guid.NewGuid());

    private async Task SeedPickAsync(Guid userId, Guid contestId, Guid groupId, string pickType)
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = "League",
            Sport = Sport.FootballNcaa,
            CommissionerUserId = Guid.NewGuid(),
            PickType = pickType,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        DataContext.UserPicks.Add(new UserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestId = contestId,
            PickemGroupId = groupId,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private async Task SeedDeviceAsync(Guid userId, bool enabled = true)
    {
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = Guid.NewGuid().ToString(),
            FcmToken = "tok",
            Platform = "ios",
            NotificationsEnabled = enabled,
            LastSeenUtc = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private void VerifySendCount(Times times) =>
        _pushSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Consume_NoMovement_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -3m, oldTotal: 50m, newTotal: 50m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_StraightUpLeague_DoesNotNotify_EvenWhenSpreadMoves()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.StraightUp);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_AtsLeague_SpreadMoved_Notifies()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_AtsLeague_OnlyTotalMoved_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldTotal: 50m, newTotal: 54m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_OverUnderLeague_TotalMoved_Notifies()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.OverUnder);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldTotal: 50m, newTotal: 54m)));

        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_SamePickerInTwoQualifyingLeagues_NotifiesOnce()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        // One device, one user, deduped across leagues -> a single push.
        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_OptedOut_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OddsChangedEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_NoEnabledDevice_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId, enabled: false);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_MissingLeagueProjection_DoesNotNotify()
    {
        // Pick exists but the PickemGroup projection hasn't landed yet — the
        // inner join drops it rather than mis-targeting.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        DataContext.UserPicks.Add(new UserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestId = contestId,
            PickemGroupId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }
}
