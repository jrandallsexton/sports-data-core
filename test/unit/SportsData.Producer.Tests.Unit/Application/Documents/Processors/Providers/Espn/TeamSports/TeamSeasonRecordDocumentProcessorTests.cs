using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports
{
    /// <summary>
    /// Tests for TeamSeasonRecordDocumentProcessor to ensure proper processing of ESPN team season record documents.
    /// </summary>
    public class TeamSeasonRecordDocumentProcessorTests
        : ProducerTestBase<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>
    {
        /// <summary>
        /// Validates that when a valid FranchiseSeason exists and a valid record document is provided,
        /// the processor creates a new FranchiseSeasonRecord with all stats, publishes an event, and persists to database.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_CreatesRecord_WhenFranchiseSeasonExists_AndValidDocument()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaTeamSeasonRecord.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IEventBus>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var savedRecords = await TeamSportDataContext.FranchiseSeasonRecords
                .Include(r => r.Stats)
                .Where(r => r.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            savedRecords.Should().HaveCount(1, "JSON document represents a single record");

            var record = savedRecords.First();
            record.Name.Should().Be("overall");
            record.Type.Should().Be("total");
            record.DisplayName.Should().Be("Overall");
            record.Summary.Should().Be("7-5");
            record.Value.Should().BeApproximately(0.5833333333333334, 0.0001);

            // Verify stats were persisted
            record.Stats.Should().NotBeNullOrEmpty();
            record.Stats.Should().HaveCountGreaterThan(10, "JSON has 23 stats");

            // Spot-check specific stat values
            var winPercentStat = record.Stats.FirstOrDefault(s => s.Name == "winPercent");
            winPercentStat.Should().NotBeNull();
            winPercentStat!.Value.Should().BeApproximately(0.5833333, 0.0001);
            winPercentStat.DisplayValue.Should().Be(".583");

            var winsStat = record.Stats.FirstOrDefault(s => s.Name == "wins");
            winsStat.Should().NotBeNull();
            winsStat!.Value.Should().Be(7.0);
            winsStat.DisplayValue.Should().Be("7");

            var lossesStat = record.Stats.FirstOrDefault(s => s.Name == "losses");
            lossesStat.Should().NotBeNull();
            lossesStat!.Value.Should().Be(5.0);

            // Verify event was published
            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordCreated>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Validates that when a FranchiseSeasonRecord already exists for the same record type,
        /// it is deleted and re-created with updated data.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_IsNoOp_WhenReprocessingIdenticalDocument()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaTeamSeasonRecord.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IEventBus>();

            // Act - Process once
            await sut.ProcessAsync(command);

            var originalId = (await TeamSportDataContext.FranchiseSeasonRecords.SingleAsync()).Id;

            // Act - Reprocess the IDENTICAL document (the backfill case)
            await sut.ProcessAsync(command);

            // Assert - one record, SAME identity, and no second event of
            // any kind: identical re-sourcing is a quiet no-op.
            var savedRecords = await TeamSportDataContext.FranchiseSeasonRecords
                .Include(r => r.Stats)
                .Where(r => r.FranchiseSeasonId == franchiseSeason.Id)
                .ToListAsync();

            savedRecords.Should().HaveCount(1);
            savedRecords[0].Id.Should().Be(originalId, "identity must survive re-sourcing");

            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordCreated>(), It.IsAny<CancellationToken>()),
                Times.Once);
            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordUpdated>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// A re-sourced document with CHANGED content (the mid-season case:
        /// records move weekly) updates the row in place — same identity,
        /// new values, an Updated event (never a second Created).
        /// </summary>
        [Fact]
        public async Task ProcessAsync_UpdatesInPlace_WhenDocumentContentChanged()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaTeamSeasonRecord.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IEventBus>();

            await sut.ProcessAsync(command);
            var originalId = (await TeamSportDataContext.FranchiseSeasonRecords.SingleAsync()).Id;

            // Act - the team won another game: summary changes 7-5 -> 8-5.
            var changedJson = json.Replace("\"summary\": \"7-5\"", "\"summary\": \"8-5\"");
            changedJson.Should().NotBe(json, "fixture must contain the 7-5 summary this test edits");

            var changedCommand = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, changedJson)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(changedCommand);

            // Assert - same row, same identity, new content, Updated event.
            var saved = await TeamSportDataContext.FranchiseSeasonRecords
                .Include(r => r.Stats)
                .SingleAsync(r => r.FranchiseSeasonId == franchiseSeason.Id);

            saved.Id.Should().Be(originalId, "update must preserve identity");
            saved.Summary.Should().Be("8-5");
            saved.Stats.Should().NotBeEmpty("stats are replaced, not dropped");

            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordCreated>(), It.IsAny<CancellationToken>()),
                Times.Once);
            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordUpdated>(), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        /// <summary>
        /// Validates that when the ParentId is not a valid GUID,
        /// the processor logs an error and does not process the document.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_DoesNothing_WhenParentIdInvalid()
        {
            // Arrange
            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, "{}")
                .With(x => x.ParentId, "not-a-guid")
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IEventBus>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonRecords.CountAsync()).Should().Be(0);
            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordCreated>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Validates that when the document JSON is null or cannot be deserialized,
        /// the processor logs an error and does not create records or publish events.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_DoesNothing_WhenDocumentIsNull()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, "null")
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();
            var bus = Mocker.GetMock<IEventBus>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            (await TeamSportDataContext.FranchiseSeasonRecords.CountAsync()).Should().Be(0);
            bus.Verify(
                x => x.Publish(It.IsAny<FranchiseSeasonRecordCreated>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// Validates that the CorrelationId from the command is persisted on the record entity.
        /// Bug #12: previously passed Guid.Empty instead of command.CorrelationId.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_PersistsCorrelationId_OnCreatedRecord()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaTeamSeasonRecord.json");
            var correlationId = Guid.NewGuid();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, correlationId)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var record = await TeamSportDataContext.FranchiseSeasonRecords.FirstAsync();
            record.CreatedBy.Should().Be(correlationId);
        }

        /// <summary>
        /// Validates that when the document contains invalid/malformed JSON,
        /// the processor throws an exception as JSON deserialization fails.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_ThrowsException_WhenInvalidJson()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, "{ invalid json }")
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();

            // Act & Assert
            await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => sut.ProcessAsync(command));
        }

        /// <summary>
        /// Validates that all stats from the JSON document are correctly persisted to the database
        /// with accurate values and metadata.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_PersistsAllStats_WithCorrectValues()
        {
            // Arrange
            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.SeasonYear, 2025)
                .With(x => x.Records, new List<FranchiseSeasonRecord>())
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaTeamSeasonRecord.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.ParentId, franchiseSeason.Id.ToString())
                .With(x => x.CorrelationId, Guid.NewGuid())
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<TeamSeasonRecordDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var record = await TeamSportDataContext.FranchiseSeasonRecords
                .Include(r => r.Stats)
                .FirstOrDefaultAsync(r => r.FranchiseSeasonId == franchiseSeason.Id);

            record.Should().NotBeNull();
            record!.Stats.Should().HaveCount(23, "JSON document has 23 stats");

            // Verify critical stats
            var expectedStats = new Dictionary<string, (double Value, string DisplayValue)>
            {
                { "wins", (7.0, "7") },
                { "losses", (5.0, "5") },
                { "ties", (0.0, "0") },
                { "winPercent", (0.5833333, ".583") },
                { "gamesPlayed", (12.0, "12") },
                { "pointsFor", (262.0, "262") },
                { "pointsAgainst", (220.0, "220") },
                { "avgPointsFor", (21.833334, "21.8") },
                { "avgPointsAgainst", (18.333334, "18.3") },
                { "differential", (42.0, "+42") }
            };

            foreach (var (statName, (expectedValue, expectedDisplayValue)) in expectedStats)
            {
                var stat = record.Stats.FirstOrDefault(s => s.Name == statName);
                stat.Should().NotBeNull($"stat '{statName}' should exist");
                stat!.Value.Should().BeApproximately(expectedValue, 0.01, $"stat '{statName}' value");
                stat.DisplayValue.Should().Be(expectedDisplayValue, $"stat '{statName}' displayValue");
            }
        }
    }
}

