using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Seasons.Queries.GetCurrentSeason;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Seasons.Queries.GetCurrentSeason;

/// <summary>
/// Pins the "current-or-upcoming" season resolution and phase projection that
/// backs the API's per-sport seasons/current resource (which the off-season
/// kickoff countdown consumes). "Current" = earliest season whose EndDate is
/// still in the future, so it must return the in-progress season during play and
/// the next upcoming season during the off-season.
/// </summary>
public class GetCurrentSeasonQueryHandlerTests : ProducerTestBase<GetCurrentSeasonQueryHandler>
{
    // Off-season "now": the 2025 season has ended, 2026 hasn't started.
    private static readonly DateTime FixedUtcNow = new(2026, 7, 17, 0, 0, 0, DateTimeKind.Utc);

    public GetCurrentSeasonQueryHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedUtcNow);
    }

    [Fact]
    public async Task ExecuteAsync_OffSeason_ReturnsNextUpcomingSeasonWithPhases()
    {
        // Arrange — a finished 2025 season and an upcoming 2026 season.
        await SeedSeasonAsync(
            year: 2025,
            start: new DateTime(2025, 8, 23, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        await SeedSeasonWithPhasesAsync(
            year: 2026,
            start: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            regularSeasonStart: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetCurrentSeasonQuery());

        // Assert — the finished 2025 season is skipped; 2026 wins.
        result.IsSuccess.Should().BeTrue();
        var season = ((Success<CurrentSeasonDto>)result).Value;
        season.SeasonYear.Should().Be(2026);
        season.Phases.Should().HaveCount(2);
        var regular = season.Phases.Single(p => p.TypeCode == 2);
        regular.StartDate.Should().Be(new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ExecuteAsync_InSeason_ReturnsInProgressSeason()
    {
        // Arrange — "now" sits inside a 2026 season that started in August and
        // runs into next January (covers the Jan–Feb playoff window that broke
        // naive UtcNow().Year).
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(new DateTime(2027, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        await SeedSeasonWithPhasesAsync(
            year: 2026,
            start: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2027, 2, 10, 0, 0, 0, DateTimeKind.Utc),
            regularSeasonStart: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetCurrentSeasonQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Success<CurrentSeasonDto>)result).Value.SeasonYear.Should().Be(2026);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleUpcomingSeasons_ReturnsEarliestByStartDate()
    {
        // Arrange — two future seasons; the earliest-starting must win.
        await SeedSeasonAsync(
            year: 2027,
            start: new DateTime(2027, 8, 28, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2028, 1, 15, 0, 0, 0, DateTimeKind.Utc));
        await SeedSeasonAsync(
            year: 2026,
            start: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetCurrentSeasonQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Success<CurrentSeasonDto>)result).Value.SeasonYear.Should().Be(2026);
    }

    [Fact]
    public async Task ExecuteAsync_NoCurrentOrUpcomingSeason_ReturnsNotFound()
    {
        // Arrange — only a season that already ended.
        await SeedSeasonAsync(
            year: 2025,
            start: new DateTime(2025, 8, 23, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetCurrentSeasonQuery());

        // Assert — legitimate not-yet-sourced case, not an error.
        result.IsSuccess.Should().BeFalse();
        ((Failure<CurrentSeasonDto>)result).Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PhasesReturnedOrderedByStartDate()
    {
        // Arrange — insert phases out of order; projection must order by StartDate.
        var seasonId = Guid.NewGuid();
        await SeedSeasonAsync(
            year: 2026,
            start: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc),
            id: seasonId);
        await SeedPhaseAsync(seasonId, typeCode: 2, name: "Regular Season",
            start: new DateTime(2026, 8, 29, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 12, 7, 0, 0, 0, DateTimeKind.Utc));
        await SeedPhaseAsync(seasonId, typeCode: 1, name: "Preseason",
            start: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc),
            end: new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Utc));

        var sut = Mocker.CreateInstance<GetCurrentSeasonQueryHandler>();

        // Act
        var result = await sut.ExecuteAsync(new GetCurrentSeasonQuery());

        // Assert
        result.IsSuccess.Should().BeTrue();
        var phases = ((Success<CurrentSeasonDto>)result).Value.Phases;
        phases.Select(p => p.TypeCode).Should().Equal(1, 2);
    }

    // ── helpers ─────────────────────────────────────────────────────────

    private async Task SeedSeasonAsync(int year, DateTime start, DateTime end, Guid? id = null)
    {
        await FootballDataContext.Seasons.AddAsync(new Season
        {
            Id = id ?? Guid.NewGuid(),
            Year = year,
            Name = $"{year} Season",
            StartDate = start,
            EndDate = end,
            CreatedUtc = Mocker.Get<IDateTimeProvider>().UtcNow(),
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
    }

    private async Task SeedSeasonWithPhasesAsync(
        int year, DateTime start, DateTime end, DateTime regularSeasonStart)
    {
        var seasonId = Guid.NewGuid();
        await SeedSeasonAsync(year, start, end, seasonId);
        await SeedPhaseAsync(seasonId, typeCode: 1, name: "Preseason",
            start: start, end: regularSeasonStart.AddDays(-1));
        await SeedPhaseAsync(seasonId, typeCode: 2, name: "Regular Season",
            start: regularSeasonStart, end: end);
    }

    private async Task SeedPhaseAsync(
        Guid seasonId, int typeCode, string name, DateTime start, DateTime end)
    {
        await FootballDataContext.SeasonPhases.AddAsync(new SeasonPhase
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            TypeCode = typeCode,
            Name = name,
            Abbreviation = name[..Math.Min(3, name.Length)].ToUpperInvariant(),
            Slug = name.ToLowerInvariant().Replace(' ', '-'),
            Year = 2026,
            StartDate = start,
            EndDate = end,
            CreatedUtc = Mocker.Get<IDateTimeProvider>().UtcNow(),
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.SaveChangesAsync();
    }
}
