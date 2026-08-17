using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class UserNotificationPreferencesUpdatedConsumerTests
    : NotificationTestBase<UserNotificationPreferencesUpdatedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    public UserNotificationPreferencesUpdatedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
    }

    private static ConsumeContext<UserNotificationPreferencesUpdated> ContextFor(
        UserNotificationPreferencesUpdated msg)
    {
        var ctx = new Mock<ConsumeContext<UserNotificationPreferencesUpdated>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static UserNotificationPreferencesUpdated MessageFor(
        Guid userId,
        bool pickResult = true,
        bool oddsChanged = true,
        string voice = "Standard")
        => new(
            userId,
            PickResultEnabled: pickResult,
            PickDeadlineReminderEnabled: true,
            ContestStartReminderEnabled: true,
            LeagueInviteEnabled: true,
            MembershipEnabled: true,
            MatchupPreviewEnabled: true,
            ScheduleChangeEnabled: true,
            OddsChangedEnabled: oddsChanged,
            PickResultVoice: voice,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());

    [Fact]
    public async Task Consume_InsertsRow_WhenNoneExists()
    {
        var userId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<UserNotificationPreferencesUpdatedConsumer>();

        await sut.Consume(ContextFor(MessageFor(userId, pickResult: false, oddsChanged: false)));

        var prefs = await DataContext.UserNotificationPreferences
            .SingleAsync(p => p.UserId == userId);
        prefs.PickResultEnabled.Should().BeFalse();
        prefs.OddsChangedEnabled.Should().BeFalse();
        prefs.MembershipEnabled.Should().BeTrue();
        prefs.CreatedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_UpdatesExistingRow_WithoutDuplicating()
    {
        var userId = Guid.NewGuid();
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedUtc = FixedNow.AddDays(-2),
            CreatedBy = Guid.NewGuid()
            // all flags default true
        });
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        var sut = Mocker.CreateInstance<UserNotificationPreferencesUpdatedConsumer>();

        await sut.Consume(ContextFor(MessageFor(userId, pickResult: false, oddsChanged: false)));

        var rows = await DataContext.UserNotificationPreferences
            .Where(p => p.UserId == userId).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].PickResultEnabled.Should().BeFalse();
        rows[0].OddsChangedEnabled.Should().BeFalse();
        rows[0].ModifiedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_SameMessageTwice_IsIdempotent()
    {
        var userId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<UserNotificationPreferencesUpdatedConsumer>();
        var msg = MessageFor(userId, pickResult: false, oddsChanged: true);

        await sut.Consume(ContextFor(msg));
        var act = async () => await sut.Consume(ContextFor(msg));
        await act.Should().NotThrowAsync();

        var rows = await DataContext.UserNotificationPreferences
            .Where(p => p.UserId == userId).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].PickResultEnabled.Should().BeFalse();
    }

    [Theory]
    [InlineData("Smack", NotificationVoice.Smack)]
    [InlineData("Standard", NotificationVoice.Standard)]
    [InlineData(null, NotificationVoice.Standard)]          // pre-field event in flight
    [InlineData("SassMaster9000", NotificationVoice.Standard)] // unknown future voice
    [InlineData("smack", NotificationVoice.Standard)]       // case-sensitive wire contract
    public async Task Consume_ProjectsVoice_WithStandardFallback(string wireVoice, NotificationVoice expected)
    {
        var userId = Guid.NewGuid();
        var sut = Mocker.CreateInstance<UserNotificationPreferencesUpdatedConsumer>();

        await sut.Consume(ContextFor(MessageFor(userId, voice: wireVoice)));

        var row = await DataContext.UserNotificationPreferences.AsNoTracking()
            .FirstAsync(p => p.UserId == userId);
        row.PickResultVoice.Should().Be(expected);
    }

    [Fact]
    public async Task Consume_PayloadOmittingPickResultVoice_DeserializesAndFallsBackToStandard()
    {
        // Contract-compatibility guard: an event serialized BEFORE the
        // PickResultVoice field existed (in-flight during a rolling deploy)
        // must deserialize with null in the positional slot and project as
        // Standard. System.Text.Json is what MassTransit uses on the wire.
        var userId = Guid.NewGuid();
        var oldPayload = $$"""
        {
            "userId": "{{userId}}",
            "pickResultEnabled": true,
            "pickDeadlineReminderEnabled": true,
            "contestStartReminderEnabled": true,
            "leagueInviteEnabled": true,
            "membershipEnabled": true,
            "matchupPreviewEnabled": true,
            "scheduleChangeEnabled": true,
            "oddsChangedEnabled": true,
            "correlationId": "{{Guid.NewGuid()}}",
            "causationId": "{{Guid.NewGuid()}}"
        }
        """;

        var msg = System.Text.Json.JsonSerializer.Deserialize<UserNotificationPreferencesUpdated>(
            oldPayload,
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        msg!.PickResultVoice.Should().BeNull("the omitted field must land as null, not throw");

        var sut = Mocker.CreateInstance<UserNotificationPreferencesUpdatedConsumer>();
        await sut.Consume(ContextFor(msg));

        var row = await DataContext.UserNotificationPreferences.AsNoTracking()
            .FirstAsync(p => p.UserId == userId);
        row.PickResultVoice.Should().Be(NotificationVoice.Standard);
    }
}
