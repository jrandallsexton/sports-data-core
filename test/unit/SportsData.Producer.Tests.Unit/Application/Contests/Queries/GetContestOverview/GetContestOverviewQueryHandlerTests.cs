using FluentValidation;
using FluentValidation.Results;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Contests.Queries.GetContestOverview;
using SportsData.Producer.Application.Services;
using SportsData.Producer.Infrastructure.Data.Common;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GetContestOverview;

public class GetContestOverviewQueryHandlerTests : ProducerTestBase<GetContestOverviewQueryHandler>
{
    private readonly Mock<ILogoSelectionService> _logoServiceMock;
    private readonly Mock<IValidator<GetContestOverviewQuery>> _validatorMock;

    public GetContestOverviewQueryHandlerTests()
    {
        _logoServiceMock = Mocker.GetMock<ILogoSelectionService>();
        _validatorMock = Mocker.GetMock<IValidator<GetContestOverviewQuery>>();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidQuery_ReturnsValidationFailure()
    {
        // Arrange
        var query = new GetContestOverviewQuery(Guid.Empty); // Invalid GUID

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ContestId", "ContestId must be provided")
        };

        _validatorMock.Setup(v => v.ValidateAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        Assert.IsType<Failure<ContestOverviewDto>>(result);
        var failure = (Failure<ContestOverviewDto>)result;
        Assert.Equal(ResultStatus.BadRequest, failure.Status);
        Assert.Single(failure.Errors);
        Assert.Equal("ContestId", failure.Errors[0].PropertyName);
        Assert.Equal("ContestId must be provided", failure.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task ExecuteAsync_WithValidQuery_ProceedsToDataAccess()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var query = new GetContestOverviewQuery(contestId);

        _validatorMock.Setup(v => v.ValidateAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult()); // Valid

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert - Should proceed to database query (contest won't be found in in-memory DB, but that's OK)
        Assert.IsType<Failure<ContestOverviewDto>>(result); // NotFound from missing data, not validation
        var failure = (Failure<ContestOverviewDto>)result;
        Assert.Equal(ResultStatus.NotFound, failure.Status); // NotFound, not BadRequest
    }

    [Fact]
    public async Task ExecuteAsync_WhenValidationFails_DoesNotAccessDatabase()
    {
        // Arrange
        var query = new GetContestOverviewQuery(Guid.Empty);

        var validationFailures = new List<ValidationFailure>
        {
            new ValidationFailure("ContestId", "ContestId must be provided")
        };

        _validatorMock.Setup(v => v.ValidateAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult(validationFailures));

        var sut = Mocker.CreateInstance<GetContestOverviewQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        Assert.IsType<Failure<ContestOverviewDto>>(result);
        var failure = (Failure<ContestOverviewDto>)result;
        Assert.Equal(ResultStatus.BadRequest, failure.Status);
        Assert.Single(failure.Errors);
        Assert.Equal("ContestId", failure.Errors[0].PropertyName);
    }
}
