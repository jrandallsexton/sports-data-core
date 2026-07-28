using FluentAssertions;

using SportsData.Api.Application.User;
using SportsData.Api.Application.User.Queries.GetUserOptions;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Queries.GetUserOptions;

public class GetUserOptionsQueryHandlerTests : ApiTestBase<GetUserOptionsQueryHandler>
{
    private static UserEntity NewUser(Guid id) => new()
    {
        Id = id,
        Username = $"user_{id:N}"[..20],
        FirebaseUid = $"uid-{id:N}",
        Email = "test@test.com",
        DisplayName = "Test User",
        SignInProvider = "test",
        LastLoginUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
    };

    private UserOption NewOption(Guid userId, string key, string value) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Key = key,
        Value = value,
        CreatedUtc = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc),
        CreatedBy = userId
    };

    [Fact]
    public async Task NoRows_YieldsDefaults()
    {
        var handler = Mocker.CreateInstance<GetUserOptionsQueryHandler>();

        var result = await handler.ExecuteAsync(new GetUserOptionsQuery { UserId = Guid.NewGuid() });

        result.IsSuccess.Should().BeTrue();
        // The inclusive default: gambling content hidden until opted in.
        result.Value.ShowGamblingContent.Should().BeFalse();
    }

    [Fact]
    public async Task Row_True_ProjectsTrue()
    {
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.UserOptions.AddAsync(
            NewOption(userId, UserOptionKeys.ShowGamblingContent, "True"));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserOptionsQueryHandler>();
        var result = await handler.ExecuteAsync(new GetUserOptionsQuery { UserId = userId });

        result.Value.ShowGamblingContent.Should().BeTrue();
    }

    [Fact]
    public async Task GarbageValue_FallsBackToDefault()
    {
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.UserOptions.AddAsync(
            NewOption(userId, UserOptionKeys.ShowGamblingContent, "not-a-bool"));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserOptionsQueryHandler>();
        var result = await handler.ExecuteAsync(new GetUserOptionsQuery { UserId = userId });

        result.Value.ShowGamblingContent.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownKeys_AreIgnored()
    {
        // Rows written by a future build must not break older code.
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.UserOptions.AddRangeAsync(
            NewOption(userId, "SomeFutureOption", "whatever"),
            NewOption(userId, UserOptionKeys.ShowGamblingContent, "True"));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserOptionsQueryHandler>();
        var result = await handler.ExecuteAsync(new GetUserOptionsQuery { UserId = userId });

        result.IsSuccess.Should().BeTrue();
        result.Value.ShowGamblingContent.Should().BeTrue();
    }

    [Fact]
    public async Task OtherUsersRows_DoNotLeak()
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        await DataContext.Users.AddRangeAsync(NewUser(userA), NewUser(userB));
        await DataContext.UserOptions.AddAsync(
            NewOption(userA, UserOptionKeys.ShowGamblingContent, "True"));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetUserOptionsQueryHandler>();
        var result = await handler.ExecuteAsync(new GetUserOptionsQuery { UserId = userB });

        result.Value.ShowGamblingContent.Should().BeFalse();
    }
}
