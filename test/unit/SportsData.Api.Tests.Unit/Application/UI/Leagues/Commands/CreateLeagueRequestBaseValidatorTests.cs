using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands;

/// <summary>
/// Pins the EndsOn-in-future rule on <see cref="CreateLeagueRequestBaseValidator{TRequest}"/>.
/// Exercised via the MLB derived validator since the rule lives in the base —
/// NCAA and NFL inherit identical behavior. Companion to the PR-C UI guards on
/// web (`min={today}` + submit guard) and mobile (Zod superRefine); this is the
/// server-side trust boundary for admin / direct-API callers.
/// </summary>
public class CreateLeagueRequestBaseValidatorTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 28, 12, 0, 0, DateTimeKind.Utc);

    private static CreateBaseballMlbLeagueRequestValidator NewValidator()
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.Setup(x => x.UtcNow()).Returns(FixedNow);
        return new CreateBaseballMlbLeagueRequestValidator(dateTimeProvider.Object);
    }

    private static CreateBaseballMlbLeagueRequest ValidBaseRequest() => new()
    {
        Name = "League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
    };

    [Fact]
    public async Task EndsOn_Null_Passes()
    {
        // Full-season leagues — no window constraint, rule is a no-op.
        var validator = NewValidator();
        var request = ValidBaseRequest();
        request.EndsOn = null;

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EndsOn_FarFuture_Passes()
    {
        var validator = NewValidator();
        var request = ValidBaseRequest();
        request.StartsOn = FixedNow.AddDays(1);
        request.EndsOn = FixedNow.AddDays(30);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EndsOn_TodayMidnightAligned_Passes()
    {
        // Midnight-aligned EndsOn normalizes to end-of-day via EffectiveEndsOn,
        // so "today" stays valid for the rest of the calendar day. Matches the
        // half-played single-day scenario the design doc calls out.
        var validator = NewValidator();
        var request = ValidBaseRequest();
        var todayMidnightUtc = new DateTime(FixedNow.Year, FixedNow.Month, FixedNow.Day, 0, 0, 0, DateTimeKind.Utc);
        request.StartsOn = todayMidnightUtc;
        request.EndsOn = todayMidnightUtc;

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task EndsOn_Yesterday_Fails()
    {
        var validator = NewValidator();
        var request = ValidBaseRequest();
        request.StartsOn = FixedNow.AddDays(-7);
        request.EndsOn = FixedNow.AddDays(-1);

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.PropertyName == nameof(CreateBaseballMlbLeagueRequest.EndsOn) &&
            e.ErrorMessage == "EndsOn can't be in the past.");
    }

    [Fact]
    public async Task EndsOn_ExactlyNow_Fails()
    {
        // Boundary: rule is strictly EffectiveEndsOn > now. A window ending
        // at the exact moment of validation has zero remaining pickable time.
        var validator = NewValidator();
        var request = ValidBaseRequest();
        request.StartsOn = FixedNow.AddDays(-1);
        request.EndsOn = FixedNow;

        var result = await validator.ValidateAsync(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(CreateBaseballMlbLeagueRequest.EndsOn));
    }
}
