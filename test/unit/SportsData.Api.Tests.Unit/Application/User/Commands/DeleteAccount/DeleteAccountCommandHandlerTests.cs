using FluentAssertions;

using Moq;

using SportsData.Api.Application.User.Commands.DeleteAccount;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.DeleteAccount;

public class DeleteAccountCommandHandlerTests : ApiTestBase<DeleteAccountCommandHandler>
{
    private static readonly DateTime FixedNow = new(2026, 7, 11, 12, 0, 0, DateTimeKind.Utc);

    public DeleteAccountCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
    }

    private async Task<Guid> SeedUserAsync()
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            FirebaseUid = "firebase-uid-abc",
            Email = "real@person.com",
            SignInProvider = "apple.com",
            DisplayName = "Real Person",
            Username = "realperson",
            Timezone = "America/New_York"
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    [Fact]
    public async Task Execute_AnonymizesUser_DeletesFirebaseUser_AndPublishes()
    {
        var userId = await SeedUserAsync();
        var handler = Mocker.CreateInstance<DeleteAccountCommandHandler>();

        var result = await handler.ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();

        // Firebase login removed (by the original uid).
        Mocker.GetMock<IFirebaseUserAdmin>().Verify(
            x => x.DeleteUserAsync("firebase-uid-abc", It.IsAny<CancellationToken>()), Times.Once());

        // Downstream purge announced.
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.Is<UserDeleted>(e => e.UserId == userId), It.IsAny<CancellationToken>()),
            Times.Once());

        var user = await DataContext.Users.FindAsync(userId);
        user!.DeletedUtc.Should().Be(FixedNow);
        user.Email.Should().NotBe("real@person.com");
        user.Email.Should().StartWith("deleted-");
        user.Username.Should().NotBe("realperson");
        user.DisplayName.Should().Be("Deleted user");
        user.FirebaseUid.Should().StartWith("deleted-");
        user.Timezone.Should().BeNull();
        user.Username.Length.Should().BeLessThanOrEqualTo(30);
    }

    [Fact]
    public async Task Execute_NotFound_WhenUserMissing()
    {
        var handler = Mocker.CreateInstance<DeleteAccountCommandHandler>();

        var result = await handler.ExecuteAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IFirebaseUserAdmin>().Verify(
            x => x.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserDeleted>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Execute_Idempotent_WhenAlreadyDeleted()
    {
        var userId = await SeedUserAsync();
        var user = await DataContext.Users.FindAsync(userId);
        user!.DeletedUtc = FixedNow.AddDays(-1);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<DeleteAccountCommandHandler>();

        var result = await handler.ExecuteAsync(userId);

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IFirebaseUserAdmin>().Verify(
            x => x.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserDeleted>(), It.IsAny<CancellationToken>()), Times.Never());
    }
}
