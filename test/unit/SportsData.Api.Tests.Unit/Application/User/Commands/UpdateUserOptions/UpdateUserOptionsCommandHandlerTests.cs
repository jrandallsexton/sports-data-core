using FluentAssertions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User;
using SportsData.Api.Application.User.Commands.UpdateUserOptions;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpdateUserOptions;

public class UpdateUserOptionsCommandHandlerTests : ApiTestBase<UpdateUserOptionsCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);

    public UpdateUserOptionsCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<IValidator<UpdateUserOptionsCommand>>(new UpdateUserOptionsCommandValidator());
    }

    private static UserEntity NewUser(Guid id) => new()
    {
        Id = id,
        Username = $"user_{id:N}"[..20],
        FirebaseUid = $"uid-{id:N}",
        Email = "test@test.com",
        DisplayName = "Test User",
        SignInProvider = "test",
        LastLoginUtc = FixedNow
    };

    [Fact]
    public async Task UnknownUser_ReturnsNotFound()
    {
        var handler = Mocker.CreateInstance<UpdateUserOptionsCommandHandler>();

        var result = await handler.ExecuteAsync(
            Guid.NewGuid(),
            new UpdateUserOptionsCommand { ShowGamblingContent = true });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task FirstChange_InsertsRow()
    {
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpdateUserOptionsCommandHandler>();
        var result = await handler.ExecuteAsync(
            userId, new UpdateUserOptionsCommand { ShowGamblingContent = true });

        result.IsSuccess.Should().BeTrue();
        var rows = await DataContext.UserOptions.Where(o => o.UserId == userId).ToListAsync();
        rows.Should().HaveCount(1);
        rows[0].Key.Should().Be(UserOptionKeys.ShowGamblingContent);
        rows[0].Value.Should().Be(true.ToString());
        rows[0].CreatedUtc.Should().Be(FixedNow);
        rows[0].ModifiedUtc.Should().BeNull();
    }

    [Fact]
    public async Task SecondChange_UpdatesInPlace()
    {
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpdateUserOptionsCommandHandler>();
        await handler.ExecuteAsync(userId, new UpdateUserOptionsCommand { ShowGamblingContent = true });
        await handler.ExecuteAsync(userId, new UpdateUserOptionsCommand { ShowGamblingContent = false });

        var rows = await DataContext.UserOptions.Where(o => o.UserId == userId).ToListAsync();
        rows.Should().HaveCount(1, "the second change must overwrite, not duplicate");
        rows[0].Value.Should().Be(false.ToString());
        rows[0].ModifiedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task UnchangedValue_DoesNotStampModified()
    {
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpdateUserOptionsCommandHandler>();
        await handler.ExecuteAsync(userId, new UpdateUserOptionsCommand { ShowGamblingContent = true });
        await handler.ExecuteAsync(userId, new UpdateUserOptionsCommand { ShowGamblingContent = true });

        var row = await DataContext.UserOptions.SingleAsync(o => o.UserId == userId);
        row.ModifiedUtc.Should().BeNull("a no-op write must not churn audit stamps");
    }

    [Fact]
    public async Task UnknownKeyRows_AreLeftUntouched()
    {
        // A row written by a future build survives a PATCH from this build.
        var userId = Guid.NewGuid();
        await DataContext.Users.AddAsync(NewUser(userId));
        await DataContext.UserOptions.AddAsync(new UserOption
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Key = "SomeFutureOption",
            Value = "precious",
            CreatedUtc = FixedNow,
            CreatedBy = userId
        });
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpdateUserOptionsCommandHandler>();
        await handler.ExecuteAsync(userId, new UpdateUserOptionsCommand { ShowGamblingContent = true });

        var rows = await DataContext.UserOptions.Where(o => o.UserId == userId).ToListAsync();
        rows.Should().HaveCount(2);
        rows.Single(r => r.Key == "SomeFutureOption").Value.Should().Be("precious");
    }
}
