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

            // Away: phases (DTO-level checks still valid)
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

            // Assert
            var saved = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams)
                .Include(o => o.Links)
                .ToListAsync();

            saved.Should().HaveCount(1);
            saved[0].CompetitionId.Should().Be(compId);

            // Teams (no snapshots)
            saved[0].Teams.Should().HaveCount(2);
            saved[0].Teams.Select(t => t.Side).Should().BeEquivalentTo(new[] { "Home", "Away" });

            // Links persisted and hrefs non-empty
            saved[0].Links.Should().NotBeEmpty();
            saved[0].Links.Should().OnlyContain(l => !string.IsNullOrWhiteSpace(l.Href));

            // Parent totals now live directly on CompetitionOdds
            saved[0].TotalPointsCurrent.Should().NotBeNull();
            saved[0].OverPriceCurrent.Should().NotBeNull();
            saved[0].UnderPriceCurrent.Should().NotBeNull();

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

            // Seed existing odds with ExternalId matching UrlHash+Provider
            var existing = dto.AsEntity(idGen.Object, compId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "content-hash");
            existing.ExternalIds = new List<CompetitionOddsExternalId>
            {
                new CompetitionOddsExternalId
                {
                    Id = Guid.NewGuid(),
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

            // Assert
            var count = await FootballDataContext.CompetitionOdds.CountAsync();
            count.Should().Be(1);
            bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task WhenExistingOddsChanged_HashDiff_Overwrites_NoDuplicates_PublishesUpdate()
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

            // Stable identity (same odds row)
            var canonicalId = Guid.NewGuid();
            idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
                 .Returns(new ExternalRefIdentity(canonicalId, "url-hash", "http://x/clean"));

            // Hashes differ between runs
            hash.SetupSequence(x => x.NormalizeAndHash(It.IsAny<string>()))
                .Returns("content-hash-v1")
                .Returns("content-hash-v2");

            var sut = Mocker.CreateInstance<EventCompetitionOddsDocumentProcessor<FootballDataContext>>();

            // ---------- CREATE ----------
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

            var afterCreate = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams)
                .Include(o => o.Links)
                .AsNoTracking()
                .SingleOrDefaultAsync();

            afterCreate.Should().NotBeNull();
            FootballDataContext.CompetitionOdds.Count().Should().Be(1);
            afterCreate!.ContentHash.Should().Be("content-hash-v1");

            // Example: verify a couple headline/current values from first payload
            afterCreate.Spread.Should().Be(-1.5m);
            afterCreate.TotalPointsCurrent.Should().NotBeNull();

            // Clear tracking
            FootballDataContext.ChangeTracker.Clear();

            // ---------- UPDATE (hash changed) ----------
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

            var saved = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams)
                .Include(o => o.Links)
                .AsNoTracking()
                .SingleOrDefaultAsync();

            saved.Should().NotBeNull();
            FootballDataContext.CompetitionOdds.Count().Should().Be(1, "no duplicate rows should be created");
            saved!.ContentHash.Should().Be("content-hash-v2");

            // Verify overwrite happened (spread changed in updated file)
            saved.Spread.Should().Be(-2.0m);

            // Teams still exactly two and updated, no snapshots involved
            saved.Teams.Should().HaveCount(2);
            saved.Teams.All(t => t.Side is "Home" or "Away").Should().BeTrue();

            // Parent totals should still be populated after update
            saved.TotalPointsCurrent.Should().NotBeNull();

            // Events: one for create, one for update
            bus.Verify(x => x.Publish(It.IsAny<object>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }

        [Fact]
        public async Task AsEntity_Maps_LineFromPointSpread_AndPriceFromSpread_SingleDto()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds_LsuOleMiss_27Sep25.json");
            var dto = json.FromJson<EspnEventCompetitionOddsDto>();
            dto.Should().NotBeNull();

            // Sanity
            dto!.Details.Should().Be("MISS -2.5");
            dto.OverUnder.Should().Be(57.5m);
            dto.Spread.Should().Be(-2.5m);
            dto.OverOdds.Should().Be(-105m);
            dto.UnderOdds.Should().Be(-115m);

            var compId = Guid.NewGuid();
            var homeFrId = Guid.NewGuid();
            var awayFrId = Guid.NewGuid();

            var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
            idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
                 .Returns(new ExternalRefIdentity(Guid.NewGuid(), "url-hash", "http://x/clean"));

            // Act
            var entity = dto.AsEntity(
                externalRefIdentityGenerator: idGen.Object,
                competitionId: compId,
                homeFranchiseSeasonId: homeFrId,
                awayFranchiseSeasonId: awayFrId,
                correlationId: Guid.NewGuid(),
                contentHash: "content-hash");

            // Assert – Parent (totals)
            entity.TotalPointsOpen.Should().Be(56.5m);
            entity.TotalPointsCurrent.Should().Be(57.5m);
            entity.OverPriceOpen.Should().Be(-110m);
            entity.UnderPriceOpen.Should().Be(-110m);
            entity.OverPriceCurrent.Should().Be(-105m);
            entity.UnderPriceCurrent.Should().Be(-115m);

            // Teams
            var home = entity.Teams.Single(t => t.Side == "Home");
            var away = entity.Teams.Single(t => t.Side == "Away");

            // --- OPEN LINES from pointSpread ---
            // away.open.pointSpread.american = "-1.5"
            away.SpreadPointsOpen.Should().Be(-1.5m);
            // home.open.pointSpread.american = "+1.5"
            home.SpreadPointsOpen.Should().Be(+1.5m);

            // --- OPEN PRICES from spread ---
            // away.open.spread.alternateDisplayValue = "-105"
            away.SpreadPriceOpen.Should().Be(-105m);
            // home.open.spread.alternateDisplayValue = "-115"
            home.SpreadPriceOpen.Should().Be(-115m);

            // --- CURRENT LINES from pointSpread ---
            away.SpreadPointsCurrent.Should().Be(+2.5m);
            home.SpreadPointsCurrent.Should().Be(-2.5m);

            // --- CURRENT PRICES: away prefers DTO.SpreadOdds when present ---
            away.SpreadPriceCurrent.Should().Be(-115m);
            home.SpreadPriceCurrent.Should().Be(-105m);

            // Moneylines (quick spot check)
            away.MoneylineOpen.Should().Be(110);
            home.MoneylineOpen.Should().Be(-130);

            // Headline mirrors
            entity.Spread.Should().Be(-2.5m);
            entity.OverUnder.Should().Be(57.5m);
            entity.OverOdds.Should().Be(-105m);
            entity.UnderOdds.Should().Be(-115m);
        }

        [Fact]
        public async Task Processor_Persists_PointSpread_FromPointSpreadBlocks_SingleDto()
        {
            // Arrange: seed contest/competition
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

            // load the SINGLE-DTO payload you pasted
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionOdds_LsuOleMiss_27Sep25.json");

            var idGen = Mocker.GetMock<IGenerateExternalRefIdentities>();
            idGen.Setup(x => x.Generate(It.IsAny<Uri>()))
                 .Returns(new ExternalRefIdentity(Guid.NewGuid(), "url-hash", "http://x/clean"));

            var hash = Mocker.GetMock<IJsonHashCalculator>();
            hash.Setup(x => x.NormalizeAndHash(It.IsAny<string>())).Returns("content-hash");

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

            // Assert from DB (what you saw in “real”)
            var saved = await FootballDataContext.CompetitionOdds
                .Include(o => o.Teams)
                .SingleAsync();

            var home = saved.Teams.Single(t => t.Side == "Home");
            var away = saved.Teams.Single(t => t.Side == "Away");

            // These MUST match the JSON pointSpread.* (lines), not spread.* (prices)
            away.SpreadPointsOpen.Should().Be(-1.5m);
            home.SpreadPointsOpen.Should().Be(+1.5m);

            away.SpreadPointsCurrent.Should().Be(+2.5m);
            home.SpreadPointsCurrent.Should().Be(-2.5m);

            // And open prices come from spread.* blocks
            away.SpreadPriceOpen.Should().Be(-105m);
            home.SpreadPriceOpen.Should().Be(-115m);
        }
    }
}