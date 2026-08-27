using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Api.Application.Athletes.Queries.GetPickemAthletes;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Athlete;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Athletes;

public class GetPickemAthletesQueryHandlerTests : UnitTestBase<GetPickemAthletesQueryHandler>
{
    private readonly Mock<IAthleteClientFactory> _athleteClientFactoryMock;
    private readonly Mock<IProvideAthletes> _athleteClientMock;

    public GetPickemAthletesQueryHandlerTests()
    {
        _athleteClientFactoryMock = Mocker.GetMock<IAthleteClientFactory>();
        _athleteClientMock = new Mock<IProvideAthletes>();
        _athleteClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_athleteClientMock.Object);

        // Real validator — an auto-mocked IValidator returns a null
        // ValidationResult and NREs before the code under test runs.
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc));
        Mocker.Use<IValidator<GetPickemAthletesQuery>>(
            new GetPickemAthletesQueryValidator(dateTimeProvider.Object));
    }

    [Fact]
    public async Task ValidRequest_RelaysTheClientResult()
    {
        var payload = new AthleteMatchupSummariesDto();
        _athleteClientMock
            .Setup(x => x.GetAthleteMatchupSummaries("QB", 2026, 1, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<AthleteMatchupSummariesDto>(payload));

        var handler = Mocker.CreateInstance<GetPickemAthletesQueryHandler>();
        var result = await handler.ExecuteAsync(
            new GetPickemAthletesQuery("football", "ncaa", "QB", 2026, 1));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(payload);
    }

    [Theory]
    [InlineData("QB", 0, 1)]    // season year nonsense
    [InlineData("QB", 2026, -1)] // negative week
    [InlineData("QB", 2026, 31)] // week above range
    [InlineData("", 2026, 1)]    // empty position
    public async Task InvalidNumericInputs_FailValidation_WithoutCallingProducer(
        string position, int seasonYear, int week)
    {
        var handler = Mocker.CreateInstance<GetPickemAthletesQueryHandler>();
        var result = await handler.ExecuteAsync(
            new GetPickemAthletesQuery("football", "ncaa", position, seasonYear, week));

        result.Status.Should().Be(ResultStatus.Validation);
        _athleteClientMock.Verify(
            x => x.GetAthleteMatchupSummaries(
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task UnsupportedSportLeague_FailsValidation()
    {
        var handler = Mocker.CreateInstance<GetPickemAthletesQueryHandler>();
        var result = await handler.ExecuteAsync(
            new GetPickemAthletesQuery("curling", "olympic", "QB", 2026, 1));

        result.Status.Should().Be(ResultStatus.Validation);
    }
}
