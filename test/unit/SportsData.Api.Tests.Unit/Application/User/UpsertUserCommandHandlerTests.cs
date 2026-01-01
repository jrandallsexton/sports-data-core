using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpsertUser;

public class UpsertUserCommandHandlerTests : ApiTestBase<UpsertUserCommandHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnBadRequest_WhenFirebaseUidIsEmpty()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "test@example.com"
        };

        // Act
        var result = await handler.ExecuteAsync(command, "", "google.com");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        result.Should().BeOfType<Failure<Guid>>();
        ((Failure<Guid>)result).Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnBadRequest_WhenEmailIsEmpty()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = ""
        };

        // Act
        var result = await handler.ExecuteAsync(command, "firebase-uid-123", "google.com");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewUser_WhenUserDoesNotExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "newuser@example.com",
            DisplayName = "Test User"
        };

        var firebaseUid = "firebase-uid-123";
        var signInProvider = "google.com";

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, signInProvider);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.Email.Should().Be(command.Email);
        user.DisplayName.Should().Be(command.DisplayName);
        user.SignInProvider.Should().Be(signInProvider);
        user.EmailVerified.Should().BeFalse();
        user.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.LastLoginUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldGenerateDisplayName_WhenNotProvided()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "newuser@example.com"
        };

        var firebaseUid = "firebase-uid-456";
        var signInProvider = "password";

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, signInProvider);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.DisplayName.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateExistingUser_WhenUserExists()
    {
        // Arrange
        var firebaseUid = "firebase-uid-789";
        var userId = Guid.NewGuid();

        var existingUser = new Infrastructure.Data.Entities.User
        {
            Id = userId,
            FirebaseUid = firebaseUid,
            Email = "old@example.com",
            DisplayName = "Old Name",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            LastLoginUtc = DateTime.UtcNow.AddDays(-5)
        };

        await DataContext.Users.AddAsync(existingUser);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "updated@example.com",
            DisplayName = "Updated Name"
        };

        var signInProvider = "google.com";

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, signInProvider);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(userId);

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId); // Same user ID
        user.Email.Should().Be(command.Email);
        user.DisplayName.Should().Be(command.DisplayName);
        user.SignInProvider.Should().Be(signInProvider);
        user.LastLoginUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        user.CreatedUtc.Should().BeCloseTo(DateTime.UtcNow.AddDays(-10), TimeSpan.FromSeconds(5)); // Unchanged
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveDisplayName_WhenNotProvidedInUpdate()
    {
        // Arrange
        var firebaseUid = "firebase-uid-999";
        var userId = Guid.NewGuid();

        var existingUser = new Infrastructure.Data.Entities.User
        {
            Id = userId,
            FirebaseUid = firebaseUid,
            Email = "old@example.com",
            DisplayName = "Original Name",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            LastLoginUtc = DateTime.UtcNow.AddDays(-5)
        };

        await DataContext.Users.AddAsync(existingUser);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "updated@example.com",
            DisplayName = null
        };

        var signInProvider = "google.com";

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, signInProvider);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.DisplayName.Should().Be("Original Name"); // Preserved
    }
}
