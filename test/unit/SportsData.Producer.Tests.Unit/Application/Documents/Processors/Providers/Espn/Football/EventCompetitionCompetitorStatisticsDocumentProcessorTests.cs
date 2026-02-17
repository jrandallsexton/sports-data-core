using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
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

            var competitionCompetitor = new CompetitionCompetitor
            {
                Id = Guid.NewGuid(),
                CompetitionId = competition.Id,
                Competition = competition,
                FranchiseSeasonId = Guid.NewGuid(),
                HomeAway = "home",
                Winner = false,
                Order = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.CompetitionCompetitors.AddAsync(competitionCompetitor);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competitionCompetitor.Id.ToString())
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

            var competitionCompetitor = new CompetitionCompetitor
            {
                Id = Guid.NewGuid(),
                CompetitionId = competition.Id,
                Competition = competition,
                FranchiseSeasonId = franchiseSeason.Id,
                HomeAway = "home",
                Winner = false,
                Order = 1,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.CompetitionCompetitors.AddAsync(competitionCompetitor);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competitionCompetitor.Id.ToString())
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

            var competitionCompetitor = new CompetitionCompetitor
            {
                Id = Guid.NewGuid(),
                CompetitionId = competition.Id,
                Competition = competition,
                FranchiseSeasonId = franchiseSeason.Id,
                HomeAway = "home",
                Winner = false,
                Order = 1,
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
            await TeamSportDataContext.CompetitionCompetitors.AddAsync(competitionCompetitor);
            await TeamSportDataContext.CompetitionCompetitorStatistics.AddAsync(existing);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competitionCompetitor.Id.ToString())
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

        [Fact]
        public async Task ProcessAsync_RequestsCompetitionCompetitor_WhenCompetitionCompetitorNotFound()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            // Enable dependency requests for this test
            var config = new SportsData.Producer.Config.DocumentProcessingConfig { EnableDependencyRequests = true };
            Mocker.Use(config);

            var bus = Mocker.GetMock<IEventBus>();

            var nonExistentCompetitionCompetitorId = Guid.NewGuid();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, nonExistentCompetitionCompetitorId.ToString())
                .With(x => x.Document, json)
                .With(x => x.AttemptCount, 0)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.Season, 2024)
                .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorStatistics)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert - Processor catches ExternalDocumentNotSourcedException and publishes DocumentRequested + retry DocumentCreated
            bus.Verify(x => x.Publish(
                It.Is<DocumentRequested>(e => e.DocumentType == DocumentType.EventCompetitionCompetitor),
                It.IsAny<CancellationToken>()), Times.Once);

            bus.Verify(x => x.Publish(
                It.Is<DocumentCreated>(e => e.AttemptCount == 1),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<CancellationToken>()), Times.Once);

            (await TeamSportDataContext.CompetitionCompetitorStatistics.CountAsync()).Should().Be(0);
        }
    }
}

