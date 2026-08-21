using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Producer.Application.Athletes.Queries.GetAthleteById;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Athletes;

/// <summary>
/// The athlete drill-down must return the full graph — athlete record,
/// seasons newest-first, per-season statistic documents newest-first with
/// categories and stats — and only THIS athlete's rows. Provenance fields
/// (doc CreatedUtc, split identifiers) are contract, not decoration: the
/// page exists to make duplicate docs and stale vintages visible.
/// </summary>
public class GetAthleteByIdQueryHandlerTests
    : ProducerTestBase<GetAthleteByIdQueryHandler>
{
    private static readonly DateTime FixedNow = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    private async Task<Guid> SeedAthleteGraphAsync()
    {
        var athleteId = Guid.NewGuid();
        var positionId = Guid.NewGuid();

        await FootballDataContext.AthletePositions.AddAsync(new AthletePosition
        {
            Id = positionId,
            Name = "Quarterback",
            DisplayName = "Quarterback",
            Abbreviation = "QB",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        await FootballDataContext.Athletes.AddAsync(new FootballAthlete
        {
            Id = athleteId,
            FirstName = "Arch",
            LastName = "Manning",
            DisplayName = "Arch Manning",
            ShortName = "A. Manning",
            Slug = "arch-manning",
            Jersey = "16",
            HeightDisplay = "6' 4\"",
            WeightDisplay = "222 lbs",
            IsActive = true,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        // Two seasons on two different franchise seasons, seeded 2024 FIRST
        // so newest-first ordering is proven, not accidental.
        var fs2024 = Guid.NewGuid();
        var fs2025 = Guid.NewGuid();
        foreach (var (fsId, year) in new[] { (fs2024, 2024), (fs2025, 2025) })
        {
            await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
            {
                Id = fsId,
                FranchiseId = Guid.NewGuid(),
                SeasonYear = year,
                Slug = "texas-longhorns",
                Location = "Texas",
                Name = "Longhorns",
                Abbreviation = "TEX",
                DisplayName = "Texas Longhorns",
                DisplayNameShort = "Texas",
                ColorCodeHex = "000000",
                IsActive = true,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }

        var season2024 = Guid.NewGuid();
        var season2025 = Guid.NewGuid();
        foreach (var (seasonId, fsId) in new[] { (season2024, fs2024), (season2025, fs2025) })
        {
            await FootballDataContext.AthleteSeasons.AddAsync(new FootballAthleteSeason
            {
                Id = seasonId,
                AthleteId = athleteId,
                FranchiseSeasonId = fsId,
                PositionId = positionId,
                FirstName = "Arch",
                LastName = "Manning",
                Jersey = "16",
                IsActive = true,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }

        // 2025 carries TWO statistic docs (older + newer — the duplicate-doc
        // shape this page must expose), the newer one with a category + stats.
        var olderDoc = Guid.NewGuid();
        var newerDoc = Guid.NewGuid();
        await FootballDataContext.AthleteSeasonStatistics.AddRangeAsync(
            new AthleteSeasonStatistic
            {
                Id = olderDoc,
                AthleteSeasonId = season2025,
                SplitId = "0",
                SplitName = "All Splits",
                SplitType = "",
                CreatedUtc = FixedNow.AddDays(-30),
                CreatedBy = Guid.NewGuid()
            },
            new AthleteSeasonStatistic
            {
                Id = newerDoc,
                AthleteSeasonId = season2025,
                SplitId = "0",
                SplitName = "All Splits",
                SplitType = "season",
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });

        var categoryId = Guid.NewGuid();
        await FootballDataContext.AthleteSeasonStatisticCategories.AddAsync(
            new AthleteSeasonStatisticCategory
            {
                Id = categoryId,
                AthleteSeasonStatisticId = newerDoc,
                Name = "passing",
                DisplayName = "Passing",
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });

        await FootballDataContext.AthleteSeasonStatisticStats.AddAsync(
            new AthleteSeasonStatisticStat
            {
                Id = Guid.NewGuid(),
                AthleteSeasonStatisticCategoryId = categoryId,
                Name = "passingYards",
                DisplayName = "Passing Yards",
                ShortDisplayName = "YDS",
                Abbreviation = "YDS",
                DisplayValue = "1,628",
                PerGameDisplayValue = "232.6",
                Value = 1628m,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });

        // A DIFFERENT athlete with a season — must not leak into the result.
        var otherAthleteId = Guid.NewGuid();
        await FootballDataContext.Athletes.AddAsync(new FootballAthlete
        {
            Id = otherAthleteId,
            FirstName = "Other",
            LastName = "Athlete",
            DisplayName = "Other Athlete",
            ShortName = "O. Athlete",
            IsActive = true,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await FootballDataContext.AthleteSeasons.AddAsync(new FootballAthleteSeason
        {
            Id = Guid.NewGuid(),
            AthleteId = otherAthleteId,
            FranchiseSeasonId = fs2025,
            PositionId = positionId,
            IsActive = true,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        await FootballDataContext.SaveChangesAsync();
        return athleteId;
    }

    [Fact]
    public async Task ReturnsFullGraph_SeasonsNewestFirst_DocsNewestFirst()
    {
        var athleteId = await SeedAthleteGraphAsync();
        var sut = Mocker.CreateInstance<GetAthleteByIdQueryHandler>();

        var result = await sut.ExecuteAsync(new GetAthleteByIdQuery(athleteId));

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;

        dto.DisplayName.Should().Be("Arch Manning");
        dto.Slug.Should().Be("arch-manning");

        // Only this athlete's seasons, newest year first.
        dto.Seasons.Should().HaveCount(2);
        dto.Seasons.Select(s => s.SeasonYear).Should().ContainInOrder(2025, 2024);
        dto.Seasons.Should().OnlyContain(s => s.TeamName == "Texas Longhorns" && s.TeamSlug == "texas-longhorns");
        dto.Seasons.Should().OnlyContain(s => s.Position == "Quarterback" && s.PositionAbbreviation == "QB");

        // Both 2025 docs surface (duplicates are the spot-check signal),
        // newest sourced first; the 2024 season has none.
        var s2025 = dto.Seasons[0];
        s2025.Statistics.Should().HaveCount(2);
        s2025.Statistics[0].CreatedUtc.Should().BeAfter(s2025.Statistics[1].CreatedUtc);
        dto.Seasons[1].Statistics.Should().BeEmpty();

        // The newer doc carries the category/stat graph.
        var passing = s2025.Statistics[0].Categories.Should().ContainSingle().Subject;
        passing.DisplayName.Should().Be("Passing");
        var yards = passing.Stats.Should().ContainSingle().Subject;
        yards.DisplayName.Should().Be("Passing Yards");
        yards.DisplayValue.Should().Be("1,628");
        yards.PerGameDisplayValue.Should().Be("232.6");
    }

    [Fact]
    public async Task UnknownAthlete_ReturnsNotFound()
    {
        await SeedAthleteGraphAsync();
        var sut = Mocker.CreateInstance<GetAthleteByIdQueryHandler>();

        var result = await sut.ExecuteAsync(new GetAthleteByIdQuery(Guid.NewGuid()));

        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
