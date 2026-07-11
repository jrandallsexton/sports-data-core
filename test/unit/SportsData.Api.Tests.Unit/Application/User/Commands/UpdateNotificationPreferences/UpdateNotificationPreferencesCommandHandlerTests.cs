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

    private static UpdateNotificationPreferencesCommand AllOff()
        => new()
        {
            PickResultEnabled = false,
            PickDeadlineReminderEnabled = false,
            ContestStartReminderEnabled = false,
            LeagueInviteEnabled = false,
            MembershipEnabled = false,
            MatchupPreviewEnabled = false,
            ScheduleChangeEnabled = false,
            OddsChangedEnabled = false
        };

    [Fact]
    public async Task Execute_CreatesRow_WhenNoneExists_AndPublishes()
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<UpdateNotificationPreferencesCommandHandler>();

        var command = AllOff();

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();

        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<UserNotificationPreferencesUpdated>(e =>
                    e.UserId == userId &&
                    e.PickResultEnabled == false &&
                    e.OddsChangedEnabled == false),
                It.IsAny<CancellationToken>()),
            Times.Once());

        DataContext.ChangeTracker.Clear();
        var prefs = DataContext.UserNotificationPreferences.Single(p => p.UserId == userId);
        prefs.PickResultEnabled.Should().BeFalse();
        prefs.OddsChangedEnabled.Should().BeFalse();
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

        var command = new UpdateNotificationPreferencesCommand
        {
            PickResultEnabled = true,
            PickDeadlineReminderEnabled = false,
            ContestStartReminderEnabled = true,
            LeagueInviteEnabled = true,
            MembershipEnabled = true,
            MatchupPreviewEnabled = true,
            ScheduleChangeEnabled = true,
            OddsChangedEnabled = false
        };

        var result = await handler.ExecuteAsync(userId, command);

        result.IsSuccess.Should().BeTrue();

        DataContext.ChangeTracker.Clear();
        var rows = DataContext.UserNotificationPreferences.Where(p => p.UserId == userId).ToList();
        rows.Should().HaveCount(1); // updated, not duplicated
        var prefs = rows.Single();
        prefs.PickDeadlineReminderEnabled.Should().BeFalse();
        prefs.OddsChangedEnabled.Should().BeFalse();
        prefs.PickResultEnabled.Should().BeTrue();
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
