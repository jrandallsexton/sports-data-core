using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Application.Reminders;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class PickemGroupWeekMatchupsGeneratedConsumerTests
    : NotificationTestBase<PickemGroupWeekMatchupsGeneratedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 9, 8, 18, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushDeviceFanout> _fanout;

    public PickemGroupWeekMatchupsGeneratedConsumerTests()
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

    private static ConsumeContext<PickemGroupWeekMatchupsGenerated> ContextFor(
        PickemGroupWeekMatchupsGenerated msg)
    {
        var ctx = new Mock<ConsumeContext<PickemGroupWeekMatchupsGenerated>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static PickemGroupWeekMatchupsGenerated Msg(Guid groupId, int week = 2, int? seasonYear = 2026)
        => new(groupId, week, null, Sport.FootballNcaa, seasonYear, Guid.NewGuid(), Guid.NewGuid());

    private async Task<(Guid GroupId, Guid UserId)> SeedLeagueWithMemberAsync(string name = "Saturday Smackdown")
    {
        var groupId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = name,
            Sport = Sport.FootballNcaa,
            CommissionerUserId = Guid.NewGuid()
        });
        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = groupId,
            UserId = userId,
            Role = "Member"
        });
        await DataContext.SaveChangesAsync();
        return (groupId, userId);
    }

    [Fact]
    public async Task Consume_LeagueMember_NotifiedWithLeagueCopyAndDeepLink()
    {
        var (groupId, userId) = await SeedLeagueWithMemberAsync();
        var msg = Msg(groupId);

        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(msg));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            userId,
            "Week 2 matchups are ready",
            It.Is<string>(b => b.Contains("Saturday Smackdown") && b.Contains("make your picks")),
            It.Is<IReadOnlyDictionary<string, string>>(d =>
                d["kind"] == "MatchupsReady" &&
                d["target"] == "picks" &&
                d["leagueId"] == groupId.ToString() &&
                d["week"] == "2" &&
                d["sport"] == "FootballNcaa")), Times.Once);

        var row = await DataContext.NotificationMatchupsReady.SingleAsync();
        row.UserId.Should().Be(userId);
        row.LeagueId.Should().Be(groupId);
        row.SeasonYear.Should().Be(2026);
        row.SeasonWeek.Should().Be(2);
        row.Result.Should().Be("Sent");
    }

    [Fact]
    public async Task Consume_UnknownLeagueProjection_SkipsQuietly()
    {
        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(Msg(Guid.NewGuid())));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);
        (await DataContext.NotificationMatchupsReady.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task Consume_OptedOutMember_SuppressedWithAuditRow()
    {
        var (groupId, userId) = await SeedLeagueWithMemberAsync();
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            MatchupsReadyEnabled = false
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId)));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Never);

        var row = await DataContext.NotificationMatchupsReady.SingleAsync();
        row.Result.Should().Be("Suppressed_UserOptedOut");
    }

    [Fact]
    public async Task Consume_MultipleMembers_EachNotified()
    {
        var (groupId, firstUserId) = await SeedLeagueWithMemberAsync();
        var secondUserId = Guid.NewGuid();
        DataContext.PickemGroupMembers.Add(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = groupId,
            UserId = secondUserId,
            Role = "Commissioner"
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId)));

        _fanout.Verify(x => x.SendToUserDevicesAsync(
            firstUserId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
        _fanout.Verify(x => x.SendToUserDevicesAsync(
            secondUserId, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>()), Times.Once);
        (await DataContext.NotificationMatchupsReady.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Consume_NoDevices_SuppressedWithAuditRow()
    {
        var (groupId, userId) = await SeedLeagueWithMemberAsync();
        _fanout
            .Setup(x => x.SendToUserDevicesAsync(
                userId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PushFanoutOutcome(PushFanoutResult.NoDevices, null));

        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId)));

        var row = await DataContext.NotificationMatchupsReady.SingleAsync();
        row.Result.Should().Be("Suppressed_NoDevice");
    }

    [Fact]
    public async Task Consume_FanoutFails_RecordsFailureReason()
    {
        var (groupId, userId) = await SeedLeagueWithMemberAsync();
        _fanout
            .Setup(x => x.SendToUserDevicesAsync(
                userId, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>()))
            .ReturnsAsync(new PushFanoutOutcome(PushFanoutResult.Failed, "android:Unavailable"));

        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId)));

        var row = await DataContext.NotificationMatchupsReady.SingleAsync();
        row.Result.Should().Be("Failed_FcmError");
        row.FailureReason.Should().Be("android:Unavailable");
    }

    [Fact]
    public async Task Consume_NullSeasonYear_ClaimsWithZeroYear()
    {
        // MatchupScheduleProcessor always passes the command's SeasonYear, but
        // the event contract allows null — the claim's dedupe key coalesces
        // to 0 rather than crashing the fan-out.
        var (groupId, _) = await SeedLeagueWithMemberAsync();

        var sut = Mocker.CreateInstance<PickemGroupWeekMatchupsGeneratedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, seasonYear: null)));

        var row = await DataContext.NotificationMatchupsReady.SingleAsync();
        row.SeasonYear.Should().Be(0);
        row.Result.Should().Be("Sent");
    }
}
