using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    public class EventCompetitionOddsDocumentProcessorTests
        : ProducerTestBase<EventCompetitionOddsDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task Test_Deserialization_SpotChecks()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");

            // Act
            var dto = json.FromJson<EspnEventCompetitionOddsDto>();

            // Assert: root
            dto.Should().NotBeNull();
            dto!.Ref!.AbsoluteUri.Should().Contain("/odds/58");
            dto.Details.Should().Be("LSU -3.5");
            dto.OverUnder.Should().Be(67.5m);
            dto.Spread.Should().Be(-3.5m);
            dto.OverOdds.Should().Be(-110m);
            dto.UnderOdds.Should().Be(-110m);
            dto.MoneylineWinner.Should().BeFalse();
            dto.SpreadWinner.Should().BeFalse();

            // Provider
            dto.Provider.Should().NotBeNull();
            dto.Provider.Ref.ToString().Should().Contain("/providers/58");
            dto.Provider.Id.Should().Be("58");
            dto.Provider.Name.Should().Be("ESPN BET");
            dto.Provider.Priority.Should().Be(1);

            // Away team odds
            dto.AwayTeamOdds.Should().NotBeNull();
            dto.AwayTeamOdds.Favorite.Should().BeFalse();
            dto.AwayTeamOdds.Underdog.Should().BeTrue();
            dto.AwayTeamOdds.MoneyLine.Should().Be(145);
            dto.AwayTeamOdds.SpreadOdds.Should().Be(-120m);
            dto.AwayTeamOdds.Team.Ref.ToString().Should().Contain("/teams/30");

            // Away: phases
            dto.AwayTeamOdds.Open.PointSpread.American.Should().Be("+6");
            dto.AwayTeamOdds.Close.PointSpread.American.Should().Be("+3.5");
            dto.AwayTeamOdds.Current.Spread.Outcome!.Type.Should().Be("win");
            dto.AwayTeamOdds.Current.MoneyLine.Outcome!.Type.Should().Be("win");

            // Home team odds
            dto.HomeTeamOdds.Should().NotBeNull();
            dto.HomeTeamOdds.Favorite.Should().BeTrue();
            dto.HomeTeamOdds.Underdog.Should().BeFalse();
            dto.HomeTeamOdds.MoneyLine.Should().Be(-170);
            dto.HomeTeamOdds.SpreadOdds.Should().Be(100m);
            dto.HomeTeamOdds.Team.Ref.ToString().Should().Contain("/teams/99");

            // Home: phases
            dto.HomeTeamOdds.Open.PointSpread.American.Should().Be("-6");
            dto.HomeTeamOdds.Close.Spread.American.Should().Be("EVEN");
            dto.HomeTeamOdds.Current.Spread.Outcome!.Type.Should().Be("loss");
            dto.HomeTeamOdds.Current.MoneyLine.Outcome!.Type.Should().Be("loss");

            // Root-level totals
            dto.Open.Total.AlternateDisplayValue.Should().Be("62.5");
            dto.Close.Total.AlternateDisplayValue.Should().Be("67.5");
            dto.Current.Under.Outcome!.Type.Should().Be("win");
            dto.Current.Total.AlternateDisplayValue.Should().Be("67.5");

            // Links (presence + valid hrefs)
            dto.Links.Should().NotBeNullOrEmpty();
            dto.Links.Should().ContainSingle(l => l.Rel.Contains("home"));
            dto.Links.Should().ContainSingle(l => l.Rel.Contains("away"));
            dto.Links.Should().ContainSingle(l => l.Rel.Contains("over"));
            dto.Links.Should().ContainSingle(l => l.Rel.Contains("under"));
            dto.Links.Should().ContainSingle(l => l.Rel.Contains("game"));
            dto.Links.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.Href.OriginalString));

            // Totals "value" is a price (decimal odds), not the line; keep expectations loose
            dto.Open.Total.Value.Should().NotBeNull();
            dto.Open.Total.Value!.Value.Should().BeGreaterThan(1.0m).And.BeLessThan(3.5m);

            // Prop bets ref (if present in fixture)
            if (dto.PropBets is not null)
                dto.PropBets.Ref.Should().NotBeNull();
        }

        [Fact]
        public async Task EspnEventCompetitionOddsDto_Deserializes_AllFieldsCorrectly()
        {
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");
            var actual = json.FromJson<EspnEventCompetitionOddsDto>();

            actual.Should().NotBeNull();
            actual.Ref!.AbsoluteUri.Should().Contain("/odds/58");

            actual.Details.Should().Be("LSU -3.5");
            actual.OverUnder.Should().Be(67.5m);
            actual.Spread.Should().Be(-3.5m);
            actual.OverOdds.Should().Be(-110m);
            actual.UnderOdds.Should().Be(-110m);
            actual.MoneylineWinner.Should().BeFalse();
            actual.SpreadWinner.Should().BeFalse();

            actual.Provider.Ref.ToString().Should().Contain("/providers/58");
            actual.Provider.Id.Should().Be("58");
            actual.Provider.Name.Should().Be("ESPN BET");
            actual.Provider.Priority.Should().Be(1);

            var a = actual.AwayTeamOdds;
            a.Favorite.Should().BeFalse();
            a.Underdog.Should().BeTrue();
            a.MoneyLine.Should().Be(145);
            a.SpreadOdds.Should().Be(-120m);
            a.Team.Ref.ToString().Should().Contain("/teams/30");
            a.Open.PointSpread.American.Should().Be("+6");
            a.Close.PointSpread.American.Should().Be("+3.5");
            a.Current.Spread.Outcome!.Type.Should().Be("win");

            var h = actual.HomeTeamOdds;
            h.Favorite.Should().BeTrue();
            h.Underdog.Should().BeFalse();
            h.MoneyLine.Should().Be(-170);
            h.SpreadOdds.Should().Be(100m);
            h.Team.Ref.ToString().Should().Contain("/teams/99");
            h.Open.PointSpread.American.Should().Be("-6");
            h.Close.Spread.American.Should().Be("EVEN");
            h.Current.MoneyLine.Outcome!.Type.Should().Be("loss");

            actual.Open.Total.AlternateDisplayValue.Should().Be("62.5");
            actual.Close.Total.AlternateDisplayValue.Should().Be("67.5");
            actual.Current.Under.Outcome!.Type.Should().Be("win");
            actual.Current.Total.AlternateDisplayValue.Should().Be("67.5");

            // Links well-formed
            actual.Links.Should().NotBeNullOrEmpty();
            actual.Links.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.Href.OriginalString));

            // Totals "value" (price) sanity
            actual.Open.Total.Value.Should().NotBeNull();
            actual.Open.Total.Value!.Value.Should().BeGreaterThan(1.0m).And.BeLessThan(3.5m);

            // Prop bets ref (if present)
            if (actual.PropBets is not null)
                actual.PropBets.Ref.Should().NotBeNull();
        }

        [Fact]
        public async Task WhenNoExistingOdds_AddsNewCompetitionOdds_Publishes()
        {
            // Arrange
            var bus = Mocker.GetMock<IEventBus>();
            var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
            var hash = Mocker.GetMock<IJsonHashCalculator>();

            var compId = Guid.NewGuid();
            var contest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.HomeTeamFranchiseSeasonId, Guid.NewGuid())
                .With(x => x.AwayTeamFranchiseSeasonId, Guid.NewGuid())
                .With(x => x.Competitions, new List<Competition>())
                .Create();

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, compId)
                .With(x => x.ContestId, contest.Id)
                .With(x => x.Contest, contest)
                .With(x => x.Odds, new List<CompetitionOdds>())
                .Create();

            await FootballDataContext.Contests.AddAsync(contest);
            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");

            idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
                .Returns(new ExternalRefIdentity(Guid.NewGuid(), "hash", "http://x/clean"));

            hash.Setup(x => x.NormalizeAndHash(It.IsAny<string>())).Returns("content-hash");

            var cmd = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, compId.ToString()) // competitionId now
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, json)
                .With(x => x.UrlHash, "url-hash")
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(cmd);

            // Assert
            var saved = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams).ThenInclude(t => t.Snapshots)
                .Include(o => o.Totals)
                .Include(o => o.Links) // ← include links now that we persist them
                .ToListAsync();

            saved.Should().HaveCount(1);
            saved[0].CompetitionId.Should().Be(compId);
            saved[0].Teams.Should().HaveCount(2);
            saved[0].Totals.Should().NotBeEmpty();

            // Links persisted and hrefs non-empty
            saved[0].Links.Should().NotBeEmpty();
            saved[0].Links.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.Href));

            // Totals "Current" snapshot parsed line matches the stored raw (american/alt)
            var currentTotals = saved[0].Totals.Single(t => t.Phase == "Current");
            currentTotals.TotalAmerican.Should().NotBeNullOrWhiteSpace();
            currentTotals.TotalValue.Should().Be(decimal.Parse(currentTotals.TotalAmerican!, System.Globalization.CultureInfo.InvariantCulture));

            bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhenExistingByUrlHashAndProvider_NoChange_NoPublish()
        {
            // Arrange
            var bus = Mocker.GetMock<IEventBus>();
            var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
            var hash = Mocker.GetMock<IJsonHashCalculator>();

            var compId = Guid.NewGuid();

            var contest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.Competitions, new List<Competition>())
                .Create();

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, compId)
                .With(x => x.ContestId, contest.Id)
                .With(x => x.Contest, contest)
                .With(x => x.Odds, new List<CompetitionOdds>())
                .Create();

            await FootballDataContext.Contests.AddAsync(contest);
            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds.json");
            var dto = json.FromJson<EspnEventCompetitionOddsDto>();

            idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
                .Returns(new ExternalRefIdentity(Guid.NewGuid(), "hash", "http://x/clean"));
            hash.Setup(x => x.NormalizeAndHash(It.IsAny<string>())).Returns("content-hash");

            // seed existing odds with ExternalId matching UrlHash+Provider
            var existing = dto.AsEntity(idGen.Object, compId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
                "content-hash");
            existing.ExternalIds = new List<CompetitionOddsExternalId>
            {
                new CompetitionOddsExternalId
                {
                    Id = existing.Id,
                    Value = "url-hash",
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = "url-hash",
                    SourceUrl = "http://x/clean"
                }
            };
            await FootballDataContext.CompetitionOdds.AddAsync(existing);
            await FootballDataContext.SaveChangesAsync();

            var cmd = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, compId.ToString())
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, json)
                .With(x => x.UrlHash, "url-hash")
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(cmd);

            // Assert: processor hits ProcessUpdate (no-op for now)
            var saved = await FootballDataContext.CompetitionOdds.CountAsync();
            saved.Should().Be(1);
            bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact(Skip = "needs review")]
        public async Task WhenExistingOddsChanged_UpdatesHeadlinesAndSnapshots_NoDuplicateRows()
        {
            // Arrange
            var bus = Mocker.GetMock<IEventBus>();
            var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
            var hash = Mocker.GetMock<IJsonHashCalculator>();

            var compId = Guid.NewGuid();
            var contest = Fixture.Build<Contest>()
                .WithAutoProperties()
                .With(x => x.HomeTeamFranchiseSeasonId, Guid.NewGuid())
                .With(x => x.AwayTeamFranchiseSeasonId, Guid.NewGuid())
                .With(x => x.Competitions, new List<Competition>())
                .Create();

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, compId)
                .With(x => x.ContestId, contest.Id)
                .With(x => x.Contest, contest)
                .With(x => x.Odds, new List<CompetitionOdds>())
                .Create();

            await FootballDataContext.Contests.AddAsync(contest);
            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            // Load fixtures
            var jsonOriginal = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds_LsuOleMiss_20Sep25.json");
            var jsonUpdated = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds_LsuOleMiss_23Sep25.json");

            // Set up stable identity
            var canonicalId = Guid.NewGuid();
            idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
                 .Returns(new ExternalRefIdentity(canonicalId, "url-hash", "http://x/clean"));

            // Set up content hashes
            hash.SetupSequence(x => x.NormalizeAndHash(It.IsAny<string>()))
                .Returns("content-hash-v1")
                .Returns("content-hash-v2");

            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            // ---------- CREATE PASS ----------
            var cmdCreate = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, compId.ToString())
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, jsonOriginal)
                .With(x => x.UrlHash, "url-hash")
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(cmdCreate);

            // Verify initial state
            var afterCreate = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams).ThenInclude(t => t.Snapshots)
                .Include(o => o.Totals)
                .Include(o => o.Links)
                .AsNoTracking()
                .SingleOrDefaultAsync();

            afterCreate.Should().NotBeNull("the create pass should have inserted one CompetitionOdds row");
            FootballDataContext.CompetitionOdds.Count().Should().Be(1, "exactly one row should exist after create");
            afterCreate!.Spread.Should().Be(-1.5m, "original headline spread should be -1.5");

            // Clear tracking state
            FootballDataContext.ChangeTracker.Clear();

            // ---------- UPDATE PASS ----------
            var cmdUpdate = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, compId.ToString())
                .With(x => x.Season, 2025)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
                .With(x => x.Document, jsonUpdated)
                .With(x => x.UrlHash, "url-hash")
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(cmdUpdate);

            // Verify final state
            var saved = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams).ThenInclude(t => t.Snapshots)
                .Include(o => o.Totals)
                .Include(o => o.Links)
                .AsNoTracking()
                .SingleOrDefaultAsync();

            saved.Should().NotBeNull("the update pass should modify the existing row, not delete it");
            FootballDataContext.CompetitionOdds.Count().Should().Be(1, "no duplicate rows should be created");
            saved!.Spread.Should().Be(-2.0m, "spread should be updated to -2.0");

            // Verify point spreads in snapshots
            var homeCurrent = saved.Teams.Single(t => t.Side == "Home").Snapshots.Single(s => s.Phase == "Current");
            var awayCurrent = saved.Teams.Single(t => t.Side == "Away").Snapshots.Single(s => s.Phase == "Current");
            homeCurrent.PointSpreadRaw.Should().Be("-1.5");
            awayCurrent.PointSpreadRaw.Should().Be("+1.5");

            // Verify snapshots count
            saved.Teams.Single(t => t.Side == "Home").Snapshots.Count.Should().Be(2);
            saved.Teams.Single(t => t.Side == "Away").Snapshots.Count.Should().Be(2);

            // Verify content hash updated
            saved.ContentHash.Should().Be("content-hash-v2");

            // Verify event published only once
            bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
