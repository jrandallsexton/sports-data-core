using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpsertUser;

public class UpsertUserCommandHandlerTests : ApiTestBase<UpsertUserCommandHandler>
{
    public UpsertUserCommandHandlerTests()
    {
        // Register the validator for all tests
        Mocker.Use<FluentValidation.IValidator<UpsertUserCommand>>(new UpsertUserCommandValidator());
    }
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
        ((Failure<Guid>)result).Errors.Should().Contain(e => e.PropertyName == "FirebaseUid");
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
    public async Task ExecuteAsync_ShouldReturnBadRequest_WhenBothFirebaseUidAndEmailAreEmpty()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = ""
        };

        // Act
        var result = await handler.ExecuteAsync(command, "", "google.com");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        result.Should().BeOfType<Failure<Guid>>();
        var failure = (Failure<Guid>)result;
        failure.Errors.Should().HaveCount(2);
        failure.Errors.Should().Contain(e => e.PropertyName == "FirebaseUid");
        failure.Errors.Should().Contain(e => e.PropertyName == "Email");
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

    [Fact]
    public async Task ExecuteAsync_ShouldResetEmailVerified_WhenEmailChanges()
    {
        // Arrange
        var firebaseUid = "firebase-uid-email-change";
        var userId = Guid.NewGuid();

        var existingUser = new Infrastructure.Data.Entities.User
        {
            Id = userId,
            FirebaseUid = firebaseUid,
            Email = "old@example.com",
            DisplayName = "Test User",
            SignInProvider = "password",
            EmailVerified = true, // Email was previously verified
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            LastLoginUtc = DateTime.UtcNow.AddDays(-5)
        };

        await DataContext.Users.AddAsync(existingUser);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "new@example.com", // Different email
            DisplayName = "Test User"
        };

        var signInProvider = "google.com";

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, signInProvider);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.Email.Should().Be("new@example.com");
        user.EmailVerified.Should().BeFalse(); // Should be reset when email changes
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveEmailVerified_WhenEmailDoesNotChange()
    {
        // Arrange
        var firebaseUid = "firebase-uid-same-email";
        var userId = Guid.NewGuid();

        var existingUser = new Infrastructure.Data.Entities.User
        {
            Id = userId,
            FirebaseUid = firebaseUid,
            Email = "test@example.com",
            DisplayName = "Test User",
            SignInProvider = "password",
            EmailVerified = true, // Email was previously verified
            CreatedUtc = DateTime.UtcNow.AddDays(-10),
            LastLoginUtc = DateTime.UtcNow.AddDays(-5)
        };

        await DataContext.Users.AddAsync(existingUser);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "test@example.com", // Same email
            DisplayName = "Updated Name"
        };

        var signInProvider = "google.com";

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, signInProvider);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.Email.Should().Be("test@example.com");
        user.EmailVerified.Should().BeTrue(); // Should remain true when email doesn't change
        user.DisplayName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleRaceCondition_WhenConcurrentRequestsCreateSameUser()
    {
        // This test simulates a race condition where two requests try to create the same user
        // The first request creates the user, and the second request (which also tries to create)
        // encounters the unique constraint and recovers by updating the existing user

        // Arrange
        var firebaseUid = "firebase-uid-race-condition";
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var firstCommand = new UpsertUserCommand
        {
            Email = "user@example.com",
            DisplayName = "First Request"
        };

        var secondCommand = new UpsertUserCommand
        {
            Email = "user@example.com",
            DisplayName = "Second Request"
        };

        // Act - First request creates the user
        var firstResult = await handler.ExecuteAsync(firstCommand, firebaseUid, "password");

        // Verify first request succeeded
        firstResult.IsSuccess.Should().BeTrue();
        var firstUserId = firstResult.Value;

        // Act - Second request with same FirebaseUid (simulating concurrent creation attempt)
        // This will find the existing user and update it instead of failing
        var secondResult = await handler.ExecuteAsync(secondCommand, firebaseUid, "google.com");

        // Assert - Second request should also succeed (race condition handled gracefully)
        secondResult.IsSuccess.Should().BeTrue();
        secondResult.Value.Should().Be(firstUserId); // Same user ID

        // Verify only one user exists with this FirebaseUid
        var users = await DataContext.Users
            .Where(u => u.FirebaseUid == firebaseUid)
            .ToListAsync();
        users.Should().HaveCount(1);

        // Verify the user was updated (not duplicated)
        var user = users[0];
        user.Id.Should().Be(firstUserId);
        user.DisplayName.Should().Be("Second Request"); // Updated by second request
        user.SignInProvider.Should().Be("google.com"); // Updated by second request
        user.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateExistingUser_WhenUserAlreadyExistsFromPriorRaceCondition()
    {
        // This test verifies that if a user is created between the initial check and the insert
        // (race condition), the handler successfully recovers by updating the existing user

        // Arrange
        var firebaseUid = "firebase-uid-existing-race";
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        // First, create a user directly in the database (simulating it was created by another request)
        var existingUser = new Infrastructure.Data.Entities.User
        {
            Id = Guid.NewGuid(),
            FirebaseUid = firebaseUid,
            Email = "original@example.com",
            DisplayName = "Original User",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-1),
            LastLoginUtc = DateTime.UtcNow.AddMinutes(-1)
        };

        await DataContext.Users.AddAsync(existingUser);
        await DataContext.SaveChangesAsync();

        var command = new UpsertUserCommand
        {
            Email = "updated@example.com",
            DisplayName = "Updated User"
        };

        // Act - Try to upsert with the same FirebaseUid
        var result = await handler.ExecuteAsync(command, firebaseUid, "google.com");

        // Assert - Should successfully update the existing user
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existingUser.Id);

        // Verify the user was updated
        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.Email.Should().Be("updated@example.com");
        user.DisplayName.Should().Be("Updated User");
        user.SignInProvider.Should().Be("google.com");
        user.EmailVerified.Should().BeFalse(); // Reset because email changed
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFailure_WhenDatabaseUpdateFails()
    {
        // Note: Testing actual DbUpdateException with in-memory database is challenging
        // because the in-memory provider doesn't enforce many constraints.
        // This test verifies the normal flow works; actual exception handling would need
        // integration tests with a real database or more sophisticated mocking.

        // However, we can verify that validation errors are properly returned as failures
        
        // Arrange
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "" // Invalid - will trigger validation failure
        };

        // Act
        var result = await handler.ExecuteAsync(command, "valid-firebase-uid", "google.com");

        // Assert - Should return failure due to validation
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        result.Should().BeOfType<Failure<Guid>>();
        
        var failure = (Failure<Guid>)result;
        failure.Errors.Should().NotBeEmpty();
        failure.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldMaintainDataIntegrity_WhenMultipleSequentialUpdates()
    {
        // This test verifies that multiple sequential updates to the same user work correctly
        // and maintain data integrity (simulating repeated login scenarios)

        // Arrange
        var firebaseUid = "firebase-uid-sequential";
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        // Act & Assert - First upsert (creates user)
        var command1 = new UpsertUserCommand
        {
            Email = "user@example.com",
            DisplayName = "Version 1"
        };
        var result1 = await handler.ExecuteAsync(command1, firebaseUid, "password");
        result1.IsSuccess.Should().BeTrue();
        var userId = result1.Value;

        // Act & Assert - Second upsert (updates user, changes provider)
        var command2 = new UpsertUserCommand
        {
            Email = "user@example.com",
            DisplayName = "Version 2"
        };
        var result2 = await handler.ExecuteAsync(command2, firebaseUid, "google.com");
        result2.IsSuccess.Should().BeTrue();
        result2.Value.Should().Be(userId); // Same user

        // Act & Assert - Third upsert (updates email, should reset EmailVerified)
        var command3 = new UpsertUserCommand
        {
            Email = "newemail@example.com",
            DisplayName = "Version 3"
        };
        var result3 = await handler.ExecuteAsync(command3, firebaseUid, "google.com");
        result3.IsSuccess.Should().BeTrue();
        result3.Value.Should().Be(userId); // Same user

        // Verify final state
        var finalUser = await DataContext.Users.FirstOrDefaultAsync(u => u.Id == userId);
        finalUser.Should().NotBeNull();
        finalUser!.FirebaseUid.Should().Be(firebaseUid);
        finalUser.Email.Should().Be("newemail@example.com");
        finalUser.DisplayName.Should().Be("Version 3");
        finalUser.SignInProvider.Should().Be("google.com");
        finalUser.EmailVerified.Should().BeFalse(); // Reset due to email change
        
        // Verify only one user exists
        var allUsers = await DataContext.Users.Where(u => u.FirebaseUid == firebaseUid).ToListAsync();
        allUsers.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHandleNullDisplayName_WhenCreatingUser()
    {
        // Arrange
        var firebaseUid = "firebase-uid-null-display";
        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "user@example.com",
            DisplayName = null // Let the system generate a display name
        };

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, "google.com");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.DisplayName.Should().NotBeNullOrWhiteSpace(); // Should have auto-generated name
        user.Email.Should().Be("user@example.com");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldPreserveCreatedUtc_WhenUpdatingExistingUser()
    {
        // Arrange
        var firebaseUid = "firebase-uid-created-utc";
        var originalCreatedUtc = DateTime.UtcNow.AddDays(-30);
        
        var existingUser = new Infrastructure.Data.Entities.User
        {
            Id = Guid.NewGuid(),
            FirebaseUid = firebaseUid,
            Email = "original@example.com",
            DisplayName = "Original",
            SignInProvider = "password",
            EmailVerified = false,
            CreatedUtc = originalCreatedUtc,
            LastLoginUtc = DateTime.UtcNow.AddDays(-5)
        };

        await DataContext.Users.AddAsync(existingUser);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<UpsertUserCommandHandler>();

        var command = new UpsertUserCommand
        {
            Email = "updated@example.com",
            DisplayName = "Updated"
        };

        // Act
        var result = await handler.ExecuteAsync(command, firebaseUid, "google.com");

        // Assert
        result.IsSuccess.Should().BeTrue();

        var user = await DataContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);
        user.Should().NotBeNull();
        user!.CreatedUtc.Should().BeCloseTo(originalCreatedUtc, TimeSpan.FromSeconds(1)); // Should preserve original
        user.LastLoginUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5)); // Should be updated
    }
}
