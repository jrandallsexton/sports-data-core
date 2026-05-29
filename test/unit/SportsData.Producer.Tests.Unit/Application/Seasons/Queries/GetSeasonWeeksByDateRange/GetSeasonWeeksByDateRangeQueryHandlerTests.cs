using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Seasons.Queries.GetSeasonWeeksByDateRange;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Seasons.Queries.GetSeasonWeeksByDateRange;

/// <summary>
/// Pins the overlap predicate + ordering + boundary-inclusion + empty/bad-input
/// behavior on the new date-range query handler. This endpoint is the
/// load-bearing dependency for the API-side league-creation redesign — windowed
/// leagues hit this to resolve which SeasonWeek(s) to bootstrap.
/// </summary>
public class GetSeasonWeeksByDateRangeQueryHandlerTests : ProducerTestBase<GetSeasonWeeksByDateRangeQueryHandler>
{
    private static readonly Guid SeasonId = Guid.NewGuid();
    private static readonly Guid SeasonPhaseId = Guid.NewGuid();
    private const int SeasonYear = 2026;

    [Fact]
    public async Task ExecuteAsync_SingleWeekFullyInsideRange_ReturnsThatWeek()
    {
        // Arrange — one week, query range strictly encloses it.
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(
            weekNumber: 1,
            startDate: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var weeks = ((Success<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result).Value;
        weeks.Should().HaveCount(1);
        weeks[0].WeekNumber.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleOverlappingWeeks_ReturnsAllOrderedByStartDate()
    {
        // Arrange — three weeks; query covers them all.
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));
        await SeedWeekAsync(3, new DateTime(2026, 9, 15, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc));
        await SeedWeekAsync(2, new DateTime(2026, 9, 8, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 14, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 9, 21, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert — defensive: insertion order was scrambled but query orders by StartDate.
        result.IsSuccess.Should().BeTrue();
        var weeks = ((Success<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result).Value;
        weeks.Select(w => w.WeekNumber).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ExecuteAsync_RangeStraddlesWeekStart_IncludesPartialOverlap()
    {
        // Arrange — query ends mid-week.
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 9, 3, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Success<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result).Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_RangeEndsExactlyOnWeekStart_IncludesWeek()
    {
        // Boundary check: inclusive overlap. A week starting at the exact
        // `To` instant still overlaps and must be returned.
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Success<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result).Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_RangeStartsExactlyOnWeekEnd_IncludesWeek()
    {
        // Mirror of the previous boundary case.
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 9, 30, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Success<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result).Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_RangeOutsideAllWeeks_ReturnsEmptySuccess()
    {
        // Row 11 in the matrix: very-far-future league whose SeasonWeeks
        // aren't sourced yet. Empty list, but Success — not Failure — so
        // the API caller can distinguish "no weeks for that range" from
        // "Producer is down."
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2027, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Success<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result).Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_FromGreaterThanTo_ReturnsBadRequestFailure()
    {
        // Flipped range is a client bug — fail fast at the boundary rather
        // than silently returning nothing (which would look identical to a
        // legitimate empty result and mask the typo).
        await SeedSeasonScaffoldingAsync();
        await SeedWeekAsync(1, new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc), new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetSeasonWeeksByDateRangeQueryHandler>();
        var query = new GetSeasonWeeksByDateRangeQuery(
            From: new DateTime(2026, 9, 7, 0, 0, 0, DateTimeKind.Utc),
            To: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        // Act
        var result = await sut.ExecuteAsync(query);

        // Assert
        result.IsSuccess.Should().BeFalse();
        var failure = (Failure<List<Core.Dtos.Canonical.CanonicalSeasonWeekDto>>)result;
        failure.Status.Should().Be(ResultStatus.BadRequest);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(GetSeasonWeeksByDateRangeQuery.From));
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task SeedSeasonScaffoldingAsync()
    {
        var season = new Season
        {
            Id = SeasonId,
            Year = SeasonYear,
            Name = $"{SeasonYear} Season",
            StartDate = new DateTime(SeasonYear, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            EndDate = new DateTime(SeasonYear + 1, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        var phase = new SeasonPhase
        {
            Id = SeasonPhaseId,
            SeasonId = SeasonId,
            Name = "Regular Season",
            Abbreviation = "REG",
            Slug = "regular-season",
            Year = SeasonYear,
            StartDate = season.StartDate,
            EndDate = season.EndDate,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Seasons.AddAsync(season);
        await FootballDataContext.SeasonPhases.AddAsync(phase);
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task SeedWeekAsync(int weekNumber, DateTime startDate, DateTime endDate)
    {
        await FootballDataContext.SeasonWeeks.AddAsync(new SeasonWeek
        {
            Id = Guid.NewGuid(),
            SeasonId = SeasonId,
            SeasonPhaseId = SeasonPhaseId,
            Number = weekNumber,
            StartDate = startDate,
            EndDate = endDate,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
    }
}
