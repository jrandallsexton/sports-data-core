using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports
{
    public class TeamSeasonStatisticsDocumentProcessorTests
        : ProducerTestBase<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>
    {
        [Fact]
        public async Task ProcessAsync_Skips_WhenFranchiseSeasonNotFound()
        {
            // Arrange
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, Guid.NewGuid().ToString())
                .With(x => x.Document, "{}")
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonStatistics.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task ProcessAsync_Skips_WhenNoCategoriesInDocument()
        {
            // Arrange
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

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonStatistics.CountAsync()).Should().Be(0);
        }

        [Fact]
        public async Task ProcessAsync_Skips_WhenNoDeltaDetected()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics_Week2.json");

            // Seed existing snapshot
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var dto = json.FromJson<EspnTeamSeasonStatisticsDto>();
            foreach (var dtoCategory in dto.Splits.Categories)
            {
                var existing = dtoCategory.AsEntity(franchiseSeason.Id);
                await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(existing);
            }
            await TeamSportDataContext.SaveChangesAsync();

            // Prepare identical command
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var data = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .ToListAsync();
            data.Count.Should().Be(dto.Splits.Categories.Count, "should not modify existing categories when no delta detected");
            var count = await TeamSportDataContext.FranchiseSeasonStatistics.CountAsync();
            count.Should().Be(dto.Splits.Categories.Count, "should not add duplicate categories when no delta detected");
        }

        [Fact]
        public async Task ProcessAsync_AddsRecords_WhenDeltaDetected()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Statistics, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonStatistics.json");

            // Seed "different" existing snapshot (e.g., different category name)
            var alteredCategory = Fixture.Build<FranchiseSeasonStatisticCategory>()
                .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
                .With(x => x.Name, "DIFFERENT")
                .With(x => x.Stats, [])
                .Create();

            await TeamSportDataContext.FranchiseSeasonStatistics.AddAsync(alteredCategory);
            await TeamSportDataContext.SaveChangesAsync();

            // Act
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonStatisticsDocumentProcessor<TeamSportDataContext>>();

            await sut.ProcessAsync(command);

            // Assert
            var data = await TeamSportDataContext.FranchiseSeasonStatistics
                .Include(x => x.Stats)
                .ToListAsync();

            var allCategories = await TeamSportDataContext.FranchiseSeasonStatistics
                .Where(c => c.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            allCategories.Should().HaveCountGreaterThan(1, "should add new categories when delta detected");
        }
    }
}
