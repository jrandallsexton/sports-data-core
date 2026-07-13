using FluentAssertions;

using SportsData.Producer.Application.Contests.Queries.GameDates;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GameDates;

// The validator gates GetGameDatesQueryHandler before it touches the DB. The
// handler itself runs raw Dapper (not InMemory-testable), but the validation
// rule is pure and covered here.
public class GetGameDatesQueryValidatorTests
{
    private readonly GetGameDatesQueryValidator _sut = new();

    private static readonly DateTime Jul10 = new(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Jul20 = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ExplicitInvertedWindow_IsInvalid()
    {
        var result = _sut.Validate(new GetGameDatesQuery(FromUtc: Jul20, ToUtc: Jul10));
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void ExplicitOrderedWindow_IsValid()
    {
        var result = _sut.Validate(new GetGameDatesQuery(FromUtc: Jul10, ToUtc: Jul20));
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void EqualBounds_IsValid()
    {
        var result = _sut.Validate(new GetGameDatesQuery(FromUtc: Jul10, ToUtc: Jul10));
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false)]   // from only
    [InlineData(false, true)]   // to only
    [InlineData(false, false)]  // fully open
    public void OpenEndedWindows_AreValid(bool hasFrom, bool hasTo)
    {
        var result = _sut.Validate(new GetGameDatesQuery(
            FromUtc: hasFrom ? Jul10 : null,
            ToUtc: hasTo ? Jul20 : null));
        result.IsValid.Should().BeTrue();
    }
}
