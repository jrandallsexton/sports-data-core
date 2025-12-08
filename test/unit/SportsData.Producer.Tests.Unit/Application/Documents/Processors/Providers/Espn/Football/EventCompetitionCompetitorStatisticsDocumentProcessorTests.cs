using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    /// <summary>
    /// Tests for EventCompetitionCompetitorStatisticsDocumentProcessor.
    /// Optimized to eliminate AutoFixture overhead.
    /// </summary>
    [Collection("Sequential")]
    public class EventCompetitionCompetitorStatisticsDocumentProcessorTests
        : ProducerTestBase<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>
    {
        [Fact]
        public async Task ProcessAsync_Throws_WhenFranchiseSeasonNotFound()
        {
            // Arrange
            // OPTIMIZATION: Direct instantiation
            var competition = new Competition
            {
                Id = Guid.NewGuid(),
                ContestId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act & Assert
            var act = () => sut.ProcessAsync(command);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task ProcessAsync_Inserts_WhenValid()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorStatisticsDto>();
            
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var identity = generator.Generate(dto!.Team.Ref);

            // OPTIMIZATION: Direct instantiation
            var franchiseSeason = new FranchiseSeason
            {
                Id = Guid.NewGuid(),
                FranchiseId = Guid.NewGuid(),
                SeasonYear = 2024,
                Abbreviation = "TEST",
                DisplayName = "Test Team",
                DisplayNameShort = "TT",
                Location = "Test",
                Name = "Test",
                Slug = "test",
                ColorCodeHex = "#FFFFFF",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Value = identity.UrlHash
                    }
                }
            };

            // OPTIMIZATION: Direct instantiation
            var competition = new Competition
            {
                Id = Guid.NewGuid(),
                ContestId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var data = await TeamSportDataContext.CompetitionCompetitorStatistics
                .Include(x => x.Categories)
                .ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x =>
                    x.FranchiseSeasonId == franchiseSeason.Id &&
                    x.CompetitionId == competition.Id);

            data.Should().NotBeNull();
            data!.Categories.Should().NotBeEmpty();
            data.Categories.SelectMany(x => x.Stats).Should().NotBeEmpty();
        }

        [Fact]
        public async Task ProcessAsync_ReplacesExisting_WhenAlreadyPresent()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorStatisticsDto>();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var identity = generator.Generate(dto!.Team.Ref);

            // OPTIMIZATION: Direct instantiation
            var franchiseSeason = new FranchiseSeason
            {
                Id = Guid.NewGuid(),
                FranchiseId = Guid.NewGuid(),
                SeasonYear = 2024,
                Abbreviation = "TEST",
                DisplayName = "Test Team",
                DisplayNameShort = "TT",
                Location = "Test",
                Name = "Test",
                Slug = "test",
                ColorCodeHex = "#FFFFFF",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds = new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Value = identity.UrlHash
                    }
                }
            };

            // OPTIMIZATION: Direct instantiation
            var competition = new Competition
            {
                Id = Guid.NewGuid(),
                ContestId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            // OPTIMIZATION: Direct instantiation
            var existing = new CompetitionCompetitorStatistic
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                CompetitionId = competition.Id,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                Categories = new List<CompetitionCompetitorStatisticCategory>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "OLD",
                        Stats = new List<CompetitionCompetitorStatisticStat>()
                    }
                }
            };

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.CompetitionCompetitorStatistics.AddAsync(existing);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var updated = await TeamSportDataContext.CompetitionCompetitorStatistics
                .Include(x => x.Categories)
                .ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x =>
                    x.FranchiseSeasonId == franchiseSeason.Id &&
                    x.CompetitionId == competition.Id);

            updated.Should().NotBeNull();
            updated!.Categories.Should().NotContain(c => c.Name == "OLD");
            updated.Categories.Should().NotBeEmpty();
            updated.Categories.SelectMany(x => x.Stats).Should().NotBeEmpty();
        }
    }
}

