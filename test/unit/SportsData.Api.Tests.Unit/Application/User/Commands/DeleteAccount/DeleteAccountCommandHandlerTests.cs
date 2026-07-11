using FluentAssertions;

using FluentValidation;

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
        Mocker.Use<IValidator<DeleteAccountCommand>>(new DeleteAccountCommandValidator());
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

        var result = await handler.ExecuteAsync(new DeleteAccountCommand { UserId = userId });

        result.IsSuccess.Should().BeTrue();

        // Firebase login removed (by the original uid).
        Mocker.GetMock<IFirebaseUserAdmin>().Verify(
            x => x.DeleteUserAsync("firebase-uid-abc", It.IsAny<CancellationToken>()), Times.Once());

        // Downstream purge announced.
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.Is<UserDeleted>(e => e.UserId == userId), It.IsAny<CancellationToken>()),
            Times.Once());

        // Reload from the store (not the tracked instance the handler mutated)
        // so these assertions validate persisted state, i.e. that SaveChanges ran.
        DataContext.ChangeTracker.Clear();
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

        var result = await handler.ExecuteAsync(new DeleteAccountCommand { UserId = Guid.NewGuid() });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IFirebaseUserAdmin>().Verify(
            x => x.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserDeleted>(), It.IsAny<CancellationToken>()), Times.Never());
    }

    [Fact]
    public async Task Execute_Idempotent_WhenAlreadyDeleted_LeavesRowUntouched()
    {
        // Seed an already-deleted (anonymized) account.
        var userId = await SeedUserAsync();
        var deletedAt = FixedNow.AddDays(-1);
        var user = await DataContext.Users.FindAsync(userId);
        user!.DeletedUtc = deletedAt;
        user.Email = "deleted-prior@deleted.invalid";
        user.Username = "del_prior";
        user.DisplayName = "Deleted user";
        user.FirebaseUid = "deleted-prior";
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<DeleteAccountCommandHandler>();

        var result = await handler.ExecuteAsync(new DeleteAccountCommand { UserId = userId });

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IFirebaseUserAdmin>().Verify(
            x => x.DeleteUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never());
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserDeleted>(), It.IsAny<CancellationToken>()), Times.Never());

        // The idempotent retry must not re-touch the row — reload from the store.
        DataContext.ChangeTracker.Clear();
        var reloaded = await DataContext.Users.FindAsync(userId);
        reloaded!.DeletedUtc.Should().Be(deletedAt);
        reloaded.Email.Should().Be("deleted-prior@deleted.invalid");
        reloaded.Username.Should().Be("del_prior");
        reloaded.DisplayName.Should().Be("Deleted user");
        reloaded.FirebaseUid.Should().Be("deleted-prior");
    }
}
