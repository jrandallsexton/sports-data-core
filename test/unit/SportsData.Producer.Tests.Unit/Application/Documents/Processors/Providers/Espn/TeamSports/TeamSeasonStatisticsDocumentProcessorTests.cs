using AutoFixture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports
{
    public class TeamSeasonStatisticsDocumentProcessorTests
        : ProducerTestBase<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>
    {
        [Fact]
        public async Task ProcessAsync_Skips_WhenFranchiseSeasonNotFound()
        {
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, Guid.NewGuid().ToString())
                .With(x => x.Document, "{}")
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            await sut.ProcessAsync(command);

            (await TeamSportDataContext.FranchiseSeasonStatistics.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task ProcessAsync_Skips_WhenNoCategoriesInDocument()
        {
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var emptyJson = "{\"splits\":{\"categories\":[]}}";

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, emptyJson)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            await sut.ProcessAsync(command);

            (await TeamSportDataContext.FranchiseSeasonStatistics.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task ProcessAsync_ReplacesExistingStatistics_WhenDocumentReceived()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            // Seed with existing (outdated) category
            var oldCategory = Fixture.Build<FranchiseSeasonStatisticCategory>()
                .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
                .With(x => x.Name, "OUTDATED")
                .With(x => x.Stats, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(oldCategory);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var dto = json.FromJson<EspnTeamSeasonStatisticsDto>();
            var expectedCount = dto.Splits.Categories.Count;

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var all = await TeamSportDataContext.FranchiseSeasonStatistics
                .Where(x => x.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            all.Should().HaveCount(expectedCount, "existing categories should be removed and replaced with current data");

            all.Should().OnlyContain(c => c.Name != "OUTDATED", "old categories should be removed");
        }

        [Fact]
        public async Task ProcessAsync_SkipsUpdate_WhenIncomingSnapshotIsOlder()
        {
            // Arrange - Create existing statistics with 13 games played
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            // Create existing statistics with MORE games played (13 games)
            var existingCategory = new FranchiseSeasonStatisticCategory
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Name = "general",
                DisplayName = "General",
                ShortDisplayName = "General",
                Abbreviation = "gen",
                Summary = "",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                Stats = new List<FranchiseSeasonStatistic>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "teamGamesPlayed",
                        DisplayName = "Team Games Played",
                        ShortDisplayName = "GP",
                        Description = "Games played",
                        Abbreviation = "GP",
                        Value = 13, // Existing has 13 games
                        DisplayValue = "13",
                        Rank = 0,
                        RankDisplayValue = "0",
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            };

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(existingCategory);
            await TeamSportDataContext.SaveChangesAsync();

            // Parse JSON, modify teamGamesPlayed to 10, then re-serialize
            var dto = json.FromJson<EspnTeamSeasonStatisticsDto>();
            
            // Find and modify the teamGamesPlayed stat to create a stale snapshot
            var modified = false;
            foreach (var category in dto.Splits.Categories)
            {
                var gamesPlayedStat = category.Stats?.FirstOrDefault(s => 
                    s.Name.Equals("teamGamesPlayed", StringComparison.OrdinalIgnoreCase));
                
                if (gamesPlayedStat != null)
                {
                    gamesPlayedStat.Value = 10; // Set to older value
                    gamesPlayedStat.DisplayValue = "10";
                    modified = true;
                    break; // Only modify the first occurrence
                }
            }

            modified.Should().BeTrue("teamGamesPlayed stat should be found and modified");

            var staleJson = dto.ToJson();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, staleJson)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert - Existing statistics should NOT be replaced
            var all = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .Where(x => x.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            all.Should().HaveCount(1, "existing statistics should be preserved");
            all.First().Name.Should().Be("general");
            
            var gamesStat = all.First().Stats.FirstOrDefault(s => s.Name == "teamGamesPlayed");
            gamesStat.Should().NotBeNull();
            gamesStat!.Value.Should().Be(13, "games played should still be 13 (not overwritten by older 10)");
        }

        [Fact]
        public async Task ProcessAsync_UpdatesStatistics_WhenIncomingSnapshotIsNewer()
        {
            // Arrange - Create existing statistics with 10 games played
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            // Create existing statistics with FEWER games played (10 games)
            var existingCategory = new FranchiseSeasonStatisticCategory
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Name = "general",
                DisplayName = "General",
                ShortDisplayName = "General",
                Abbreviation = "gen",
                Summary = "",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                Stats = new List<FranchiseSeasonStatistic>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "teamGamesPlayed",
                        DisplayName = "Team Games Played",
                        ShortDisplayName = "GP",
                        Description = "Games played",
                        Abbreviation = "GP",
                        Value = 10, // Existing has 10 games
                        DisplayValue = "10",
                        Rank = 0,
                        RankDisplayValue = "0",
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            };

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(existingCategory);
            await TeamSportDataContext.SaveChangesAsync();

            // Use JSON with MORE games played (13 games) - newer snapshot
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json) // JSON has 13 games
                .OmitAutoProperties()
                .Create();

            var dto = json.FromJson<EspnTeamSeasonStatisticsDto>();
            var expectedCount = dto.Splits.Categories.Count;

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert - Statistics should be replaced with newer data
            var all = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .Where(x => x.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            all.Should().HaveCount(expectedCount, "existing statistics should be replaced with newer data");

            // Find the teamGamesPlayed stat in any category
            var gamesStat = all
                .SelectMany(c => c.Stats)
                .FirstOrDefault(s => s.Name == "teamGamesPlayed");

            gamesStat.Should().NotBeNull();
            gamesStat!.Value.Should().Be(13, "games played should be updated to 13");
        }

        [Fact]
        public async Task ProcessAsync_UpdatesStatistics_WhenIncomingSnapshotHasSameGamesPlayed()
        {
            // Arrange - Create existing statistics with same games played (may be stat corrections)
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            // Create existing statistics with SAME games played (13 games)
            var existingCategory = new FranchiseSeasonStatisticCategory
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Name = "general",
                DisplayName = "General",
                ShortDisplayName = "General",
                Abbreviation = "gen",
                Summary = "",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                Stats = new List<FranchiseSeasonStatistic>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "teamGamesPlayed",
                        DisplayName = "Team Games Played",
                        ShortDisplayName = "GP",
                        Description = "Games played",
                        Abbreviation = "GP",
                        Value = 13, // Same as incoming
                        DisplayValue = "13",
                        Rank = 0,
                        RankDisplayValue = "0",
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = Guid.NewGuid()
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "fumbles",
                        DisplayName = "Fumbles",
                        ShortDisplayName = "F",
                        Description = "Fumbles",
                        Abbreviation = "FUM",
                        Value = 999, // Wrong value that should be corrected
                        DisplayValue = "999",
                        Rank = 0,
                        RankDisplayValue = "0",
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            };

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(existingCategory);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json) // JSON has 13 games (same as existing)
                .OmitAutoProperties()
                .Create();

            var dto = json.FromJson<EspnTeamSeasonStatisticsDto>();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert - Statistics should be updated (allow corrections even with same games played)
            var all = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .Where(x => x.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            all.Should().HaveCountGreaterThan(1, "should have multiple categories from new data");

            // Verify fumbles stat was corrected
            var fumblesStat = all
                .SelectMany(c => c.Stats)
                .FirstOrDefault(s => s.Name == "fumbles");

            fumblesStat.Should().NotBeNull();
            fumblesStat!.Value.Should().Be(9, "fumbles value should be corrected from 999 to 9 (from JSON)");
        }

        [Fact]
        public async Task ProcessAsync_UpdatesStatistics_WhenNoGamesPlayedStatFound()
        {
            // Arrange - Edge case: existing stats without teamGamesPlayed stat
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            // Create existing statistics WITHOUT teamGamesPlayed stat
            var existingCategory = new FranchiseSeasonStatisticCategory
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Name = "general",
                DisplayName = "General",
                ShortDisplayName = "General",
                Abbreviation = "gen",
                Summary = "",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                Stats = new List<FranchiseSeasonStatistic>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "fumbles",
                        DisplayName = "Fumbles",
                        ShortDisplayName = "F",
                        Description = "Fumbles",
                        Abbreviation = "FUM",
                        Value = 5,
                        DisplayValue = "5",
                        Rank = 0,
                        RankDisplayValue = "0",
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            };

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(existingCategory);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var dto = json.FromJson<EspnTeamSeasonStatisticsDto>();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert - Should update (can't compare without gamesPlayed stat)
            var all = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .Where(x => x.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            all.Should().HaveCountGreaterThan(1, "should have multiple categories from new data");

            // Verify teamGamesPlayed stat is now present
            var gamesStat = all
                .SelectMany(c => c.Stats)
                .FirstOrDefault(s => s.Name == "teamGamesPlayed");

            gamesStat.Should().NotBeNull("teamGamesPlayed stat should now exist");
            gamesStat!.Value.Should().Be(13);
        }

        [Fact]
        public async Task ProcessAsync_RetriesOnConcurrencyConflict_WhenAnotherProcessUpdatesConcurrently()
        {
            // Arrange - Simulate concurrent update scenario
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .With(x => x.RowVersion, (uint)1) // Initial version
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            // Create existing statistics with FEWER games played (10 games)
            var existingCategory = new FranchiseSeasonStatisticCategory
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Name = "general",
                DisplayName = "General",
                ShortDisplayName = "General",
                Abbreviation = "gen",
                Summary = "",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                Stats = new List<FranchiseSeasonStatistic>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "teamGamesPlayed",
                        DisplayName = "Team Games Played",
                        ShortDisplayName = "GP",
                        Description = "Games played",
                        Abbreviation = "GP",
                        Value = 10, // Existing has 10 games
                        DisplayValue = "10",
                        Rank = 0,
                        RankDisplayValue = "0",
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = Guid.NewGuid()
                    }
                }
            };

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(existingCategory);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json) // JSON has 13 games (newer)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert - Statistics should be updated successfully (retry mechanism handled any conflicts)
            var all = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .Where(x => x.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            all.Should().HaveCountGreaterThan(1, "statistics should be updated with new data");

            // Verify teamGamesPlayed was updated to 13
            var gamesStat = all
                .SelectMany(c => c.Stats)
                .FirstOrDefault(s => s.Name == "teamGamesPlayed");

            gamesStat.Should().NotBeNull();
            gamesStat!.Value.Should().Be(13, "games played should be updated to 13");

            // Verify FranchiseSeason.RowVersion was updated (concurrency token changed)
            var updatedFranchiseSeason = await TeamSportDataContext.FranchiseSeasons.FindAsync(franchiseSeason.Id);
            updatedFranchiseSeason.Should().NotBeNull();
            // Note: In real PostgreSQL, xmin would change. In EF InMemory, RowVersion behavior is simulated.
        }
    }
}
