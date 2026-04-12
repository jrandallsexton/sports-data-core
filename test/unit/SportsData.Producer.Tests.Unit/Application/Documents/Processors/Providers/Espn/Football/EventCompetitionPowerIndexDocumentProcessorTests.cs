using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    /// <summary>
    /// Tests for EventCompetitionPowerIndexDocumentProcessor.
    /// Optimized to eliminate AutoFixture overhead.
    /// </summary>
    [Collection("Sequential")]
    public class EventCompetitionPowerIndexDocumentProcessorTests :
        ProducerTestBase<EventCompetitionPowerIndexDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenCompetitionExists_IndexesAreAdded()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPowerIndex.json");

            var competitionId = Guid.NewGuid();
            
            // OPTIMIZATION: Direct instantiation
            var competition = new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var franchiseSeasonId = Guid.NewGuid();
            
            // OPTIMIZATION: Direct instantiation
            var franchiseSeason = new FranchiseSeason
            {
                Id = franchiseSeasonId,
                FranchiseId = Guid.NewGuid(),
                SeasonYear = 2024,
                Abbreviation = "TEAM",
                DisplayName = "Team",
                DisplayNameShort = "T",
                Location = "Location",
                Name = "Team",
                Slug = "team",
                ColorCodeHex = "#FFFFFF",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var identity = new ExternalRefIdentityGenerator().Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en");

            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Provider = SourceDataProvider.Espn,
                SourceUrl = identity.CleanUrl,
                SourceUrlHash = identity.UrlHash,
                Value = identity.UrlHash
            });

            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/powerindex/99?lang=en".UrlHash())
                .With(x => x.DocumentType, DocumentType.EventCompetitionPowerIndex)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, competition.Id.ToString())
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionPowerIndexDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var result = await FootballDataContext.CompetitionPowerIndexes
                .Include(x => x.PowerIndex)
                .Where(x => x.CompetitionId == competition.Id)
                .ToListAsync();

            result.Should().NotBeEmpty();
            result.All(x => x.FranchiseSeasonId == franchiseSeason.Id).Should().BeTrue();
        }

        [Fact]
        public async Task WhenReprocessed_DoesNotCreateDuplicates()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPowerIndex.json");

            var competitionId = Guid.NewGuid();

            var competition = new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var franchiseSeasonId = Guid.NewGuid();

            var franchiseSeason = new FranchiseSeason
            {
                Id = franchiseSeasonId,
                FranchiseId = Guid.NewGuid(),
                SeasonYear = 2024,
                Abbreviation = "TEAM",
                DisplayName = "Team",
                DisplayNameShort = "T",
                Location = "Location",
                Name = "Team",
                Slug = "team",
                ColorCodeHex = "#FFFFFF",
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid()
            };

            var identity = new ExternalRefIdentityGenerator().Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en");

            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Provider = SourceDataProvider.Espn,
                SourceUrl = identity.CleanUrl,
                SourceUrlHash = identity.UrlHash,
                Value = identity.UrlHash
            });

            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/powerindex/99?lang=en".UrlHash())
                .With(x => x.DocumentType, DocumentType.EventCompetitionPowerIndex)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, competition.Id.ToString())
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionPowerIndexDocumentProcessor<FootballDataContext>>();

            // Act — process twice with change tracker cleared between calls to emulate separate requests
            await sut.ProcessAsync(command);
            FootballDataContext.ChangeTracker.Clear();
            await sut.ProcessAsync(command);

            // Assert — should have exactly the same count as processing once (4 stats in test data)
            var result = await FootballDataContext.CompetitionPowerIndexes
                .Where(x => x.CompetitionId == competition.Id)
                .ToListAsync();

            result.Should().HaveCount(4);
        }

        [Fact]
        public async Task WhenCompetitionNotFound_DoesNotCreatePowerIndexes()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionPowerIndex.json");

            var nonExistentCompetitionId = Guid.NewGuid();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/powerindex/99?lang=en".UrlHash())
                .With(x => x.DocumentType, DocumentType.EventCompetitionPowerIndex)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, nonExistentCompetitionId.ToString())
                .With(x => x.AttemptCount, 0)
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionPowerIndexDocumentProcessor<FootballDataContext>>();

            // Act — base class catches ExternalDocumentNotSourcedException and schedules retry
            await sut.ProcessAsync(command);

            // Assert — no power indexes created
            var result = await FootballDataContext.CompetitionPowerIndexes
                .Where(x => x.CompetitionId == nonExistentCompetitionId)
                .ToListAsync();

            result.Should().BeEmpty();

            // Assert — retry was published with incremented AttemptCount and RetryReason header
            Mock.Get(Mocker.Get<IEventBus>())
                .Verify(x => x.Publish(
                    It.Is<DocumentCreated>(dc => dc.AttemptCount == 1),
                    It.Is<IDictionary<string, object>>(h => h.ContainsKey("RetryReason")),
                    It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}

