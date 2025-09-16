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
    }
}
