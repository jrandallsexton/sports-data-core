using FluentAssertions;

using SportsData.Api.Application.Admin.Queries.GetMatchupPreview;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.Queries.GetMatchupPreview;

public class GetMatchupPreviewQueryHandlerTests : ApiTestBase<GetMatchupPreviewQueryHandler>
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnValidationError_WhenContestIdIsEmpty()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery(Guid.Empty);

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        result.Should().BeOfType<Failure<string>>();
        ((Failure<string>)result).Errors.Should().Contain(e => e.PropertyName == nameof(query.ContestId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnNotFound_WhenPreviewDoesNotExist()
    {
        // Arrange
        var handler = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var contestId = Guid.NewGuid();
        var query = new GetMatchupPreviewQuery(contestId);

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Should().BeOfType<Failure<string>>();
        ((Failure<string>)result).Errors.Should().Contain(e => e.PropertyName == nameof(query.ContestId));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnJson_WhenPreviewExists()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PromptVersion = "v1.0",
            Overview = "Test preview content",
            PredictedStraightUpWinner = Guid.NewGuid(),
            PredictedSpreadWinner = Guid.NewGuid(),
            OverUnderPrediction = OverUnderPrediction.Over,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await DataContext.MatchupPreviews.AddAsync(preview);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery(contestId);

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Should().BeOfType<Success<string>>();
        var json = ((Success<string>)result).Value;
        json.Should().NotBeNullOrWhiteSpace();
        json.Should().Contain(contestId.ToString());
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnFirstPreview_WhenMultiplePreviewsExist()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var preview1 = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PromptVersion = "v1.0",
            Overview = "First preview",
            PredictedStraightUpWinner = Guid.NewGuid(),
            PredictedSpreadWinner = Guid.NewGuid(),
            OverUnderPrediction = OverUnderPrediction.Over,
            CreatedUtc = DateTime.UtcNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        };

        var preview2 = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PromptVersion = "v2.0",
            Overview = "Second preview",
            PredictedStraightUpWinner = Guid.NewGuid(),
            PredictedSpreadWinner = Guid.NewGuid(),
            OverUnderPrediction = OverUnderPrediction.Under,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await DataContext.MatchupPreviews.AddAsync(preview1);
        await DataContext.MatchupPreviews.AddAsync(preview2);
        await DataContext.SaveChangesAsync();

        var handler = Mocker.CreateInstance<GetMatchupPreviewQueryHandler>();
        var query = new GetMatchupPreviewQuery(contestId);

        // Act
        var result = await handler.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var json = ((Success<string>)result).Value;
        json.Should().NotBeNullOrWhiteSpace();
    }
}
