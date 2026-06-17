#nullable enable
using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    /// <summary>
    /// Tests for EventCompetitionStatusDocumentProcessor.
    /// Optimized to eliminate AutoFixture overhead.
    /// </summary>
    [Collection("Sequential")]
    public class EventCompetitionStatusDocumentProcessorTests :
        ProducerTestBase<EventCompetitionStatusDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenCompetitionExists_StatusIsAdded()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionStatus.json");

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

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/status?lang=en".UrlHash())
                .With(x => x.DocumentType, DocumentType.EventCompetitionStatus)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, competition.Id.ToString())
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionStatusDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var result = await FootballDataContext.CompetitionStatuses
                .Where(x => x.CompetitionId == competition.Id)
                .ToListAsync();

            result.Should().ContainSingle();

            var status = result.First();
            status.StatusTypeName.Should().Be("STATUS_FINAL");
            status.DisplayClock.Should().Be("0:00");
            status.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task WhenStatusIsCanceled_StampsContestCancelledUtc()
        {
            // Arrange — seed a Contest + Competition, run the processor with a
            // STATUS_CANCELED status doc, expect Contest.CancelledUtc populated.
            var fixedNow = new DateTime(2026, 6, 16, 14, 0, 0, DateTimeKind.Utc);
            Mocker.GetMock<IDateTimeProvider>()
                .Setup(p => p.UtcNow())
                .Returns(fixedNow);

            var (contest, competition, command) = await SeedAndBuildCommandAsync(
                statusJsonName: "STATUS_CANCELED");

            var sut = Mocker.CreateInstance<EventCompetitionStatusDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert — Contest.CancelledUtc stamped with the deterministic clock.
            var refreshed = await FootballDataContext.Contests
                .FirstAsync(c => c.Id == contest.Id);
            refreshed.CancelledUtc.Should().Be(fixedNow);
        }

        [Fact]
        public async Task WhenStatusIsCanceledButCancelledUtcAlreadySet_PreservesOriginal()
        {
            // Arrange — Contest already has CancelledUtc from a prior observation.
            // A redundant STATUS_CANCELED doc must not refresh the timestamp.
            var fixedNow = new DateTime(2026, 6, 16, 14, 0, 0, DateTimeKind.Utc);
            var originalCancelledUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
            Mocker.GetMock<IDateTimeProvider>()
                .Setup(p => p.UtcNow())
                .Returns(fixedNow);

            var (contest, _, command) = await SeedAndBuildCommandAsync(
                statusJsonName: "STATUS_CANCELED",
                priorStatusTypeName: "STATUS_CANCELED",
                contestCancelledUtc: originalCancelledUtc);

            var sut = Mocker.CreateInstance<EventCompetitionStatusDocumentProcessor<FootballDataContext>>();

            await sut.ProcessAsync(command);

            var refreshed = await FootballDataContext.Contests
                .FirstAsync(c => c.Id == contest.Id);
            refreshed.CancelledUtc.Should().Be(originalCancelledUtc,
                "first-observed cancellation timestamp is preserved across redundant doc refreshes");
        }

        [Fact]
        public async Task WhenStatusTransitionsAwayFromCanceled_LeavesCancelledUtcInPlace()
        {
            // Arrange — ESPN reverses a cancellation (rare, but possible).
            // Audit-as-irrevocable: CancelledUtc remains stamped and the
            // processor logs a warning. ContestEnrichmentJob continues to
            // skip the contest.
            var fixedNow = new DateTime(2026, 6, 16, 14, 0, 0, DateTimeKind.Utc);
            var originalCancelledUtc = new DateTime(2026, 6, 10, 9, 0, 0, DateTimeKind.Utc);
            Mocker.GetMock<IDateTimeProvider>()
                .Setup(p => p.UtcNow())
                .Returns(fixedNow);

            var (contest, _, command) = await SeedAndBuildCommandAsync(
                statusJsonName: "STATUS_SCHEDULED",
                priorStatusTypeName: "STATUS_CANCELED",
                contestCancelledUtc: originalCancelledUtc);

            var sut = Mocker.CreateInstance<EventCompetitionStatusDocumentProcessor<FootballDataContext>>();

            await sut.ProcessAsync(command);

            var refreshed = await FootballDataContext.Contests
                .FirstAsync(c => c.Id == contest.Id);
            refreshed.CancelledUtc.Should().Be(originalCancelledUtc,
                "cancellation is treated as irrevocable; only an admin override should clear it");
        }

        /// <summary>
        /// Builds a STATUS_FINAL-shaped JSON payload by string-replacing
        /// the type.name and type.description on the canonical test fixture.
        /// Sufficient for the cancellation-lifecycle tests; the rest of the
        /// payload shape (refs, clock, period) isn't material to the
        /// stamping logic under test.
        /// </summary>
        private async Task<string> BuildStatusJsonAsync(string statusTypeName)
        {
            var baseJson = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionStatus.json");
            return baseJson
                .Replace("\"STATUS_FINAL\"", $"\"{statusTypeName}\"")
                .Replace("\"Final\"", "\"" + statusTypeName + "\"");
        }

        private async Task<(FootballContest contest, FootballCompetition competition, ProcessDocumentCommand command)>
            SeedAndBuildCommandAsync(
                string statusJsonName,
                string? priorStatusTypeName = null,
                DateTime? contestCancelledUtc = null)
        {
            Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());

            // Each caller has already set up IDateTimeProvider.UtcNow() with
            // a fixed value before invoking this helper. Capture it once
            // and reuse for every seeded entity's timestamp so the test
            // doesn't accidentally depend on real wall-clock time.
            var seededUtc = Mocker.Get<IDateTimeProvider>().UtcNow();

            var contest = new FootballContest
            {
                Id = Guid.NewGuid(),
                Name = "Test Contest",
                ShortName = "TC",
                Sport = Sport.FootballNcaa,
                SeasonYear = 2025,
                StartDateUtc = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                HomeTeamFranchiseSeasonId = Guid.NewGuid(),
                AwayTeamFranchiseSeasonId = Guid.NewGuid(),
                CreatedUtc = seededUtc,
                CreatedBy = Guid.NewGuid(),
                CancelledUtc = contestCancelledUtc
            };

            var competition = new FootballCompetition
            {
                Id = Guid.NewGuid(),
                ContestId = contest.Id,
                Date = seededUtc,
                CreatedUtc = seededUtc,
                CreatedBy = Guid.NewGuid()
            };

            await FootballDataContext.Contests.AddAsync(contest);
            await FootballDataContext.Competitions.AddAsync(competition);

            if (!string.IsNullOrEmpty(priorStatusTypeName))
            {
                var priorStatus = new FootballCompetitionStatus
                {
                    Id = Guid.NewGuid(),
                    CompetitionId = competition.Id,
                    StatusTypeId = "0",
                    StatusTypeName = priorStatusTypeName,
                    StatusState = "post",
                    StatusDescription = priorStatusTypeName,
                    StatusDetail = priorStatusTypeName,
                    StatusShortDetail = priorStatusTypeName,
                    IsCompleted = priorStatusTypeName == "STATUS_FINAL" || priorStatusTypeName == "STATUS_CANCELED",
                    CreatedBy = Guid.NewGuid(),
                    CreatedUtc = seededUtc
                };
                await FootballDataContext.CompetitionStatuses.AddAsync(priorStatus);
            }

            await FootballDataContext.SaveChangesAsync();
            FootballDataContext.ChangeTracker.Clear();

            var json = await BuildStatusJsonAsync(statusJsonName);
            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, json)
                .With(x => x.UrlHash,
                    "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/status?lang=en".UrlHash())
                .With(x => x.DocumentType, DocumentType.EventCompetitionStatus)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, competition.Id.ToString())
                .Create();

            return (contest, competition, command);
        }
    }
}

