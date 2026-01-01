using FluentAssertions;

using SportsData.Api.Application.User.Commands.UpsertUser;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.User.Commands.UpsertUser;

public class UpsertUserCommandValidatorTests
{
    private readonly UpsertUserCommandValidator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Email_Is_Empty()
    {
        // Arrange
        var command = new UpsertUserCommand { Email = "" };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Whitespace()
    {
        // Arrange
        var command = new UpsertUserCommand { Email = "   " };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email");
    }

    [Fact]
    public void Should_Have_Error_When_Email_Is_Invalid_Format()
    {
        // Arrange
        var command = new UpsertUserCommand { Email = "invalid-email" };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Email" && e.ErrorMessage.Contains("valid email"));
    }

    [Fact]
    public void Should_Not_Have_Error_When_Email_Is_Valid()
    {
        // Arrange
        var command = new UpsertUserCommand { Email = "test@example.com" };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
