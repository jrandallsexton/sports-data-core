using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Seasons;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class SeasonPollWeekCreatedConsumerTests : NotificationTestBase<SeasonPollWeekCreatedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushDeviceFanout> _fanout;

    public SeasonPollWeekCreatedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);

        _fanout = Mocker.GetMock<IPushDeviceFanout>();
        _fanout
            .Setup(x => x.SendToUserDevicesAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PushFanoutOutcome(PushFanoutResult.Sent, null));
    }

    private static ConsumeContext<SeasonPollWeekCreated> ContextFor(SeasonPollWeekCreated msg)
    {
        var ctx = new Mock<ConsumeContext<SeasonPollWeekCreated>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static SeasonPollWeekCreated Msg(
        string? pollSlug = "ap",
        Guid? seasonWeekId = null,
        Sport sport = Sport.FootballNcaa,
        Guid? seasonPollWeekId = null)
        => new(
            SeasonPollWeekId: seasonPollWeekId ?? Guid.NewGuid(),
            SeasonPollId: Guid.NewGuid(),
            SeasonWeekId: seasonWeekId,
            SeasonWeekStartDate: seasonWeekId is null ? null : FixedNow,
            SeasonWeekEndDate: seasonWeekId is null ? null : FixedNow.AddDays(7),
            SeasonYear: 2026,
            PollSlug: pollSlug,
            Ref: null,
            Sport: sport,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

    private async Task<Guid> SeedMemberAsync(Sport sport = Sport.FootballNcaa, Guid? userId = null, Guid? groupId = null)
    {
        var uid = userId ?? Guid.NewGuid();
        var gid = groupId ?? Guid.NewGuid();

        if (!await DataContext.PickemGroups.AnyAsync(g => g.Id == gid))
        {
            DataContext.PickemGroups.Add(new PickemGroup
            {
                Id = gid,
                Name = "Saturday Smackdown",
                Sport = sport,
                CommissionerUserId = Guid.NewGuid()
            });
        }

        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = gid,
            UserId = uid,
            Role = "Member"
        });
        await DataContext.SaveChangesAsync();
        return uid;
    }

    [Fact]
    public async Task Consume_ApPoll_NotifiesLeagueMemberWithDeepLink()
    {
        var userId = await SeedMemberAsync();
        var msg = Msg();

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(msg));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            userId,
            "AP Top 25 is out",
            It.Is<string>(b => b.Contains("rankings just dropped")),
            It.Is<IReadOnlyDictionary<string, string>>(d =>
                d["kind"] == "PollReleased" &&
                d["target"] == "rankings" &&
                d["sport"] == "FootballNcaa")), Times.Once);

        var row = await DataContext.NotificationPollReleases.SingleAsync();
        row.UserId.Should().Be(userId);
        row.SeasonPollWeekId.Should().Be(msg.SeasonPollWeekId);
        row.Result.Should().Be("Sent");
        row.Channel.Should().Be("Fcm");
        row.Title.Should().Be("AP Top 25 is out");
    }

    [Fact]
    public async Task Consume_NonApPoll_StaysSilent()
    {
        await SeedMemberAsync();

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg(pollSlug: "usa")));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
        (await DataContext.NotificationPollReleases.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Consume_NullSeasonWeekId_StillNotifies()
    {
        // The SeasonPollWeek → SeasonWeek linkage is known-unreliable
        // (preseason poll linked wrong / null). The broadcast must not be
        // gated on it — this pins the "notify regardless of week mapping"
        // decision from docs/features/poll-release-notifications.md.
        var userId = await SeedMemberAsync();

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg(seasonWeekId: null)));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            userId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
    }

    [Fact]
    public async Task Consume_OptedOutUser_SuppressedWithAuditRow()
    {
        var userId = await SeedMemberAsync();
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            PollReleasedEnabled = false
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg()));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);

        var row = await DataContext.NotificationPollReleases.SingleAsync();
        row.Result.Should().Be("Suppressed_UserOptedOut");
    }

    [Fact]
    public async Task Consume_MemberOfOtherSportOnly_NotInAudience()
    {
        await SeedMemberAsync(sport: Sport.BaseballMlb);

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg(sport: Sport.FootballNcaa)));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
        (await DataContext.NotificationPollReleases.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Consume_MemberOfTwoLeagues_NotifiedOnce()
    {
        // Broadcast is per USER, not per membership — the audience query's
        // Distinct() is load-bearing and this pins it.
        var userId = Guid.NewGuid();
        await SeedMemberAsync(userId: userId);
        await SeedMemberAsync(userId: userId);

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg()));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            userId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
        (await DataContext.NotificationPollReleases.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Consume_NoDevices_SuppressedWithAuditRow()
    {
        var userId = await SeedMemberAsync();
        _fanout
            .Setup(x => x.SendToUserDevicesAsync(
                userId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PushFanoutOutcome(PushFanoutResult.NoDevices, null));

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg()));

        var row = await DataContext.NotificationPollReleases.SingleAsync();
        row.Result.Should().Be("Suppressed_NoDevice");
    }

    [Fact]
    public async Task Consume_FanoutFails_RecordsFailureReason()
    {
        var userId = await SeedMemberAsync();
        _fanout
            .Setup(x => x.SendToUserDevicesAsync(
                userId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PushFanoutOutcome(PushFanoutResult.Failed, "ios:Unregistered"));

        var sut = Mocker.CreateInstance<SeasonPollWeekCreatedConsumer>();
        await sut.Consume(ContextFor(Msg()));

        var row = await DataContext.NotificationPollReleases.SingleAsync();
        row.Result.Should().Be("Failed_FcmError");
        row.FailureReason.Should().Be("ios:Unregistered");
    }
}
