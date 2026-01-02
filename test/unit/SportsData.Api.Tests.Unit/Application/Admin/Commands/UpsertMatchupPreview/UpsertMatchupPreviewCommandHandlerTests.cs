using FluentAssertions;
using FluentValidation;

using SportsData.Api.Application.Admin.Commands.UpsertMatchupPreview;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Commands.UpsertMatchupPreview;

public class UpsertMatchupPreviewCommandHandlerTests : ApiTestBase<UpsertMatchupPreviewCommandHandler>
{
    public UpsertMatchupPreviewCommandHandlerTests()
    {
        // Register validator
        Mocker.Use<IValidator<UpsertMatchupPreviewCommand>>(new UpsertMatchupPreviewCommandValidator());
    }
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenJsonContentIsEmpty()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertMatchupPreviewCommandHandler>();
        var command = new UpsertMatchupPreviewCommand("");

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        result.Should().BeOfType<Failure<Guid>>();
        var failure = (Failure<Guid>)result;
        failure.Errors.Should().Contain(e => e.PropertyName == nameof(command.JsonContent));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenJsonContentIsInvalid()
    {
        // Arrange
        var handler = Mocker.CreateInstance<UpsertMatchupPreviewCommandHandler>();
        var command = new UpsertMatchupPreviewCommand("invalid json");

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var failure = (Failure<Guid>)result;
        failure.Errors.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewPreview_WhenNoExistingPreviewExists()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var previewId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        // Create JSON with camelCase for deserialization (matches FromJson DefaultOptions)
        var json = $$"""
        {
            "id": "{{previewId}}",
            "contestId": "{{contestId}}",
            "promptVersion": "v1.0",
            "overview": "Test overview",
            "analysis": "Test analysis",
            "prediction": "Test prediction",
            "predictedStraightUpWinner": "{{Guid.NewGuid()}}",
            "predictedSpreadWinner": "{{Guid.NewGuid()}}",
            "overUnderPrediction": 1,
            "createdUtc": "{{DateTime.UtcNow:O}}",
            "createdBy": "{{userId}}"
        }
        """;

        var command = new UpsertMatchupPreviewCommand(json);
        var handler = Mocker.CreateInstance<UpsertMatchupPreviewCommandHandler>();

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Success<Guid>>();
        var success = (Success<Guid>)result;
        success.Value.Should().Be(contestId);

        // Verify it was saved
        var saved = await DataContext.MatchupPreviews.FindAsync(previewId);
        saved.Should().NotBeNull();
        saved!.ContestId.Should().Be(contestId);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReplaceExistingPreview_WhenPreviewAlreadyExists()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var existingPreviewId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        
        var existingPreview = new MatchupPreview
        {
            Id = existingPreviewId,
            ContestId = contestId,
            PromptVersion = "v1.0",
            Overview = "Old overview",
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = userId
        };

        await DataContext.MatchupPreviews.AddAsync(existingPreview);
        await DataContext.SaveChangesAsync();

        var newPreviewId = Guid.NewGuid();
        var json = $$"""
        {
            "id": "{{newPreviewId}}",
            "contestId": "{{contestId}}",
            "promptVersion": "v2.0",
            "overview": "New overview",
            "analysis": "New analysis",
            "createdUtc": "{{DateTime.UtcNow:O}}",
            "createdBy": "{{userId}}"
        }
        """;

        var command = new UpsertMatchupPreviewCommand(json);
        var handler = Mocker.CreateInstance<UpsertMatchupPreviewCommandHandler>();

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var success = (Success<Guid>)result;
        success.Value.Should().Be(contestId);

        // Verify old was removed and new was added
        var oldSaved = await DataContext.MatchupPreviews.FindAsync(existingPreviewId);
        oldSaved.Should().BeNull();

        var newSaved = await DataContext.MatchupPreviews.FindAsync(newPreviewId);
        newSaved.Should().NotBeNull();
        newSaved!.Overview.Should().Be("New overview");
        newSaved.PromptVersion.Should().Be("v2.0");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenSaveChangesFails()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PromptVersion = "v1.0",
            Overview = "Test overview",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        var json = System.Text.Json.JsonSerializer.Serialize(preview);
        var command = new UpsertMatchupPreviewCommand(json);

        // Dispose the context to cause save to fail
        await DataContext.DisposeAsync();

        var handler = Mocker.CreateInstance<UpsertMatchupPreviewCommandHandler>();

        // Act
        var result = await handler.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var failure = (Failure<Guid>)result;
        failure.Errors.Should().Contain(e => e.PropertyName == "Error");
    }
}
