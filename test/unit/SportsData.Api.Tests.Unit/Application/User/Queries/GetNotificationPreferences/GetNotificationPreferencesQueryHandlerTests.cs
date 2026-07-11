using FluentAssertions;

using SportsData.Api.Application.User.Queries.GetNotificationPreferences;
using SportsData.Core.Common;

using Xunit;

using PrefsEntity = SportsData.Api.Infrastructure.Data.Entities.UserNotificationPreferences;

namespace SportsData.Api.Tests.Unit.Application.User.Queries.GetNotificationPreferences;

public class GetNotificationPreferencesQueryHandlerTests
    : ApiTestBase<GetNotificationPreferencesQueryHandler>
{
    [Fact]
    public async Task Execute_ReturnsAllEnabledDefaults_WhenNoRow()
    {
        var handler = Mocker.CreateInstance<GetNotificationPreferencesQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetNotificationPreferencesQuery { UserId = Guid.NewGuid() });

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.PickResultEnabled.Should().BeTrue();
        dto.PickDeadlineReminderEnabled.Should().BeTrue();
        dto.ContestStartReminderEnabled.Should().BeTrue();
        dto.LeagueInviteEnabled.Should().BeTrue();
        dto.MembershipEnabled.Should().BeTrue();
        dto.MatchupPreviewEnabled.Should().BeTrue();
        dto.ScheduleChangeEnabled.Should().BeTrue();
        dto.OddsChangedEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Execute_ReturnsSavedValues_WhenRowExists()
    {
        var userId = Guid.NewGuid();
        await DataContext.UserNotificationPreferences.AddAsync(new PrefsEntity
        {
            UserId = userId,
            PickResultEnabled = false,
            OddsChangedEnabled = false
            // remaining flags default true
        });
        await DataContext.SaveChangesAsync();
        DataContext.ChangeTracker.Clear();

        var handler = Mocker.CreateInstance<GetNotificationPreferencesQueryHandler>();

        var result = await handler.ExecuteAsync(
            new GetNotificationPreferencesQuery { UserId = userId });

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.PickResultEnabled.Should().BeFalse();
        dto.OddsChangedEnabled.Should().BeFalse();
        dto.MembershipEnabled.Should().BeTrue();
    }
}
