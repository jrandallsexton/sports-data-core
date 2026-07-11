using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Api.Application.User.Commands.UpdateNotificationPreferences;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

using Xunit;

using PrefsEntity = SportsData.Api.Infrastructure.Data.Entities.UserNotificationPreferences;
using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpdateNotificationPreferences;

public class UpdateNotificationPreferencesCommandHandlerTests
    : ApiTestBase<UpdateNotificationPreferencesCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    public UpdateNotificationPreferencesCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IValidator<UpdateNotificationPreferencesCommand>>(
            new UpdateNotificationPreferencesCommandValidator());
    }

    private async Task<Guid> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            FirebaseUid = $"uid-{id:N}",
            Email = "real@person.com",
            SignInProvider = "apple.com",
            DisplayName = "Real Person",
            Username = $"user{id:N}"[..12]
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    // A deliberately asymmetric flag pattern (T F T T F T F T). Because no two
    // adjacent flags share a value in the same way, a mis-mapped line in the
    // handler's Apply() (e.g. copying command.ScheduleChange into
    // OddsChangedEnabled) flips at least one assertion.
    private static UpdateNotificationPreferencesCommand DistinctFlags()
        => new()
        {
            PickResultEnabled = true,
            PickDeadlineReminderEnabled = false,
            ContestStartReminderEnabled = true,
            LeagueInviteEnabled = true,
            MembershipEnabled = false,
            MatchupPreviewEnabled = true,
            ScheduleChangeEnabled = false,
            OddsChangedEnabled = true
        };

    // Assert all eight flags on the persisted entity match the command.
    private static void AssertFlags(PrefsEntity prefs, UpdateNotificationPreferencesCommand c)
    {
        prefs.PickResultEnabled.Should().Be(c.PickResultEnabled);
        prefs.PickDeadlineReminderEnabled.Should().Be(c.PickDeadlineReminderEnabled);
        prefs.ContestStartReminderEnabled.Should().Be(c.ContestStartReminderEnabled);
        prefs.LeagueInviteEnabled.Should().Be(c.LeagueInviteEnabled);
        prefs.MembershipEnabled.Should().Be(c.MembershipEnabled);
        prefs.MatchupPreviewEnabled.Should().Be(c.MatchupPreviewEnabled);
        prefs.ScheduleChangeEnabled.Should().Be(c.ScheduleChangeEnabled);
        prefs.OddsChangedEnabled.Should().Be(c.OddsChangedEnabled);
    }

    // All eight flags on the published event match the command (plus UserId).
    private static bool EventMatches(
        UserNotificationPreferencesUpdated e, Guid userId, UpdateNotificationPreferencesCommand c)
        => e.UserId == userId
           && e.PickResultEnabled == c.PickResultEnabled
           && e.PickDeadlineReminderEnabled == c.PickDeadlineReminderEnabled
           && e.ContestStartReminderEnabled == c.ContestStartReminderEnabled
           && e.LeagueInviteEnabled == c.LeagueInviteEnabled
           && e.MembershipEnabled == c.MembershipEnabled
           && e.MatchupPreviewEnabled == c.MatchupPreviewEnabled
           && e.ScheduleChangeEnabled == c.ScheduleChangeEnabled
           && e.OddsChangedEnabled == c.OddsChangedEnabled;

    [Fact]
    public async Task Execute_CreatesRow_WhenNoneExists_AndPublishes()
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var command = DistinctFlags();

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();

        // Event carries the complete flag set + userId, published exactly once.
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<UserNotificationPreferencesUpdated>(e => EventMatches(e, userId, command)),
                It.IsAny<CancellationToken>()),
            Times.Once());

        DataContext.ChangeTracker.Clear();
        var prefs = DataContext.UserNotificationPreferences.Single(p => p.UserId == userId);
        AssertFlags(prefs, command);
        prefs.CreatedUtc.Should().Be(FixedNow);
        prefs.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Execute_UpdatesExistingRow_InPlace()
    {
        var userId = await SeedUserAsync();
        await DataContext.UserNotificationPreferences.AddAsync(new PrefsEntity
        {
            UserId = userId,
            CreatedUtc = FixedNow.AddDays(-3),
            CreatedBy = userId
            // all flags default true
        });
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var command = DistinctFlags();

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();

        // Update path still publishes the complete flag set exactly once.
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<UserNotificationPreferencesUpdated>(e => EventMatches(e, userId, command)),
                It.IsAny<CancellationToken>()),
            Times.Once());

        DataContext.ChangeTracker.Clear();
        var rows = DataContext.UserNotificationPreferences.Where(p => p.UserId == userId).ToList();
        rows.Should().HaveCount(1); // updated, not duplicated
        var prefs = rows.Single();
        AssertFlags(prefs, command);
        prefs.ModifiedUtc.Should().Be(FixedNow);
        prefs.ModifiedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Execute_NotFound_WhenUserMissing()
    {
        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var result = await handler.ExecuteAsync(Guid.NewGuid(), new UpdateNotificationPreferencesCommand());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserNotificationPreferencesUpdated>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }
}
