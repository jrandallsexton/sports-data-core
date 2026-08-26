using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Contests.Queries.Matchups.GetMatchupsForSeasonWeek;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries;

/// <summary>
/// The validator is the only guard between a bad week/phase value and a
/// silent empty slate (out-of-range values match no SeasonWeek rows —
/// they don't error). The handler itself is a thin Dapper pass-through
/// and is not unit-tested; the validator carries the behavior worth
/// pinning.
/// </summary>
public class GetMatchupsForSeasonWeekQueryValidatorTests
{
    private readonly GetMatchupsForSeasonWeekQueryValidator _validator;

    public GetMatchupsForSeasonWeekQueryValidatorTests()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc));
        _validator = new GetMatchupsForSeasonWeekQueryValidator(dateTimeProvider.Object);
    }

    [Theory]
    [InlineData(1)]  // Preseason
    [InlineData(2)]  // Regular Season
    [InlineData(3)]  // Postseason
    [InlineData(4)]  // Off Season
    public void KnownPhaseTypeCodes_AreValid(int typeCode)
    {
        var result = _validator.Validate(new GetMatchupsForSeasonWeekQuery(2026, 4, typeCode));

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(5)]
    [InlineData(999)]
    [InlineData(-1)]
    public void UnknownPhaseTypeCodes_AreRejected(int typeCode)
    {
        var result = _validator.Validate(new GetMatchupsForSeasonWeekQuery(2026, 4, typeCode));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e =>
            e.PropertyName == nameof(GetMatchupsForSeasonWeekQuery.SeasonPhaseTypeCode));
    }

    [Fact]
    public void OmittedPhase_DefaultsToRegularSeason_AndIsValid()
    {
        var query = new GetMatchupsForSeasonWeekQuery(2026, 4);

        query.SeasonPhaseTypeCode.Should().Be(2);
        _validator.Validate(query).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(1999, 4)]   // year too early
    [InlineData(2028, 4)]   // more than one year out (clock fixed at 2026)
    [InlineData(2026, 0)]   // week below range
    [InlineData(2026, 31)]  // week above range
    public void OutOfRangeYearOrWeek_IsRejected(int year, int week)
    {
        var result = _validator.Validate(new GetMatchupsForSeasonWeekQuery(year, week));

        result.IsValid.Should().BeFalse();
    }
}
