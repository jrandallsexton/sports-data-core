using SportsData.Producer.Application.Contests.Queries.GetContestOverview;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GetContestOverview;

public class GetContestOverviewQueryValidatorTests : UnitTestBase<GetContestOverviewQueryValidator>
{
    [Fact]
    public void Validate_WithEmptyGuid_ReturnsValidationError()
    {
        // Arrange
        var query = new GetContestOverviewQuery(Guid.Empty);
        var validator = new GetContestOverviewQueryValidator();

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Equal("ContestId", result.Errors[0].PropertyName);
        Assert.Equal("ContestId must be provided", result.Errors[0].ErrorMessage);
    }

    [Fact]
    public void Validate_WithValidGuid_ReturnsSuccess()
    {
        // Arrange
        var query = new GetContestOverviewQuery(Guid.NewGuid());
        var validator = new GetContestOverviewQueryValidator();

        // Act
        var result = validator.Validate(query);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
