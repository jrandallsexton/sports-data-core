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

    private async Task<Prompt> SeedPromptAsync()
    {
        var prompt = new Prompt
        {
            Id = Guid.NewGuid(),
            Name = $"test-prompt-{Guid.NewGuid():N}",
            WithStats = false,
            Text = "TEST PROMPT"
        };
        await DataContext.Prompts.AddAsync(prompt);
        await DataContext.SaveChangesAsync();
        return prompt;
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCreateNewPreview_WhenNoExistingPreviewExists()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var previewId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var prompt = await SeedPromptAsync();

        // Create JSON with camelCase for deserialization (matches FromJson DefaultOptions)
        var json = $$"""
        {
            "id": "{{previewId}}",
            "contestId": "{{contestId}}",
            "promptId": "{{prompt.Id}}",
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
        
        var prompt = await SeedPromptAsync();
        var existingPreview = new MatchupPreview
        {
            Id = existingPreviewId,
            ContestId = contestId,
            PromptId = prompt.Id,
            Overview = "Old overview",
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = userId
        };

        await DataContext.MatchupPreviews.AddAsync(existingPreview);
        await DataContext.SaveChangesAsync();

        var newPrompt = await SeedPromptAsync();
        var newPreviewId = Guid.NewGuid();
        var json = $$"""
        {
            "id": "{{newPreviewId}}",
            "contestId": "{{contestId}}",
            "promptId": "{{newPrompt.Id}}",
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
        newSaved.PromptId.Should().Be(newPrompt.Id);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenPromptIdMissingOrUnknown()
    {
        // Arrange — non-nullable FK: an upsert without a valid promptId must
        // fail with a clear validation message, not an opaque FK violation.
        var json = $$"""
        {
            "id": "{{Guid.NewGuid()}}",
            "contestId": "{{Guid.NewGuid()}}",
            "promptId": "{{Guid.NewGuid()}}",
            "overview": "No such prompt",
            "createdUtc": "{{DateTime.UtcNow:O}}",
            "createdBy": "{{Guid.NewGuid()}}"
        }
        """;

        var handler = Mocker.CreateInstance<UpsertMatchupPreviewCommandHandler>();

        // Act
        var result = await handler.ExecuteAsync(new UpsertMatchupPreviewCommand(json), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        ((Failure<Guid>)result).Errors.Should().Contain(e => e.PropertyName == nameof(MatchupPreview.PromptId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnError_WhenSaveChangesFails()
    {
        // Arrange — prompt seeded BEFORE disposal so the PromptId check
        // passes and the failure surfaces at save, per the test's intent.
        var contestId = Guid.NewGuid();
        var prompt = await SeedPromptAsync();
        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PromptId = prompt.Id,
            Overview = "Test overview",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        // ToJson mirrors the handler's FromJson conventions (camelCase) —
        // raw PascalCase Serialize would leave PromptId unbound and trip
        // the validation before the save this test targets.
        var json = SportsData.Core.Extensions.JsonExtensions.ToJson(preview);
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
