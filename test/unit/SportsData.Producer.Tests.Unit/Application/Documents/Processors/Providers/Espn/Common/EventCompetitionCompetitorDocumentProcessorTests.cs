using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    [Collection("Sequential")]
    public class EventCompetitionCompetitorDocumentProcessorTests
        : ProducerTestBase<FootballEventCompetitionCompetitorDocumentProcessor<FootballDataContext>>
    {
        private static readonly DateTime FixedTestNow = new(2026, 5, 4, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public async Task WhenJsonIsValid_DtoDeserializes()
        {
            // arrange
            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionCompetitor.json");

            // act
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            // assert
            dto.Should().NotBeNull();

            // factual assertions based on the test JSON

        }

        [Fact]
        public async Task WhenEntityAlreadyExists_AndDesignationsChanged_ShouldSyncHomeAwayOrderWinner()
        {
            // A neutral-site home/away re-designation must flow onto the
            // existing competitor row — previously the update path logged
            // dto.HomeAway but never wrote it, freezing rows at first
            // ingestion. Fixture: homeAway=home, order=0, winner=false.

            // arrange
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);
            var sut = Mocker.CreateInstance<FootballEventCompetitionCompetitorDocumentProcessor<FootballDataContext>>();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionCompetitor.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            var competitorIdentity = generator.Generate(dto!.Ref);
            var teamIdentity = generator.Generate(dto.Team.Ref);

            var competitionId = Guid.NewGuid();
            await FootballDataContext.Competitions.AddAsync(new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = FixedTestNow,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });

            var franchiseSeasonId = Guid.NewGuid();
            await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
            {
                Id = franchiseSeasonId,
                Abbreviation = "TST",
                DisplayName = "Test FS",
                DisplayNameShort = "TFS",
                Slug = teamIdentity.CanonicalId.ToString(),
                Location = "Test Location",
                Name = "Test Franchise Season",
                ColorCodeHex = "#FFFFFF",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new FranchiseSeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = teamIdentity.CleanUrl,
                        SourceUrlHash = teamIdentity.UrlHash,
                        Value = teamIdentity.UrlHash
                    }
                ]
            });

            // Existing competitor row with STALE designations (opposite of the doc).
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = competitorIdentity.CanonicalId,
                CompetitionId = competitionId,
                FranchiseSeasonId = franchiseSeasonId,
                HomeAway = "away",
                Order = 5,
                Winner = true,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new CompetitionCompetitorExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitorIdentity.CleanUrl,
                        SourceUrlHash = competitorIdentity.UrlHash,
                        Value = competitorIdentity.UrlHash
                    }
                ]
            });
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.ParentId, competitionId.ToString())
                .With(x => x.UrlHash, competitorIdentity.UrlHash)
                .OmitAutoProperties()
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert — designations now track the document
            var saved = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .Where(x => x.Id == competitorIdentity.CanonicalId)
                .Select(x => new { x.HomeAway, x.Order, x.Winner })
                .FirstAsync();
            saved.HomeAway.Should().Be("home");
            saved.Order.Should().Be(0);
            saved.Winner.Should().BeFalse();
        }

        [Fact]
        public void SwapParkingValue_FitsHomeAwayColumn()
        {
            // HomeAway is varchar(10) and the InMemory provider does NOT
            // enforce column lengths — the first deploy shipped a 12-char
            // parking value and every full-swap document failed with
            // Postgres 22001. This pins the length where the provider can't.
            EventCompetitionCompetitorDocumentProcessorBase<FootballDataContext>
                .SwapParkingValue.Length.Should().BeLessThanOrEqualTo(10);
        }

        [Fact]
        public async Task WhenNewCompetitorClaimsOccupiedSide_ShouldRelocateStaleOccupant()
        {
            // The 2026-08-29 Howard @ Alabama A&M jam: ESPN re-designated
            // home/away, our DB held the OLD home occupant, and the new
            // competitor's insert collided with the (CompetitionId, HomeAway)
            // unique index forever. The occupant must be relocated to the
            // vacant opposite side before the insert.

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);
            var sut = Mocker.CreateInstance<FootballEventCompetitionCompetitorDocumentProcessor<FootballDataContext>>();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionCompetitor.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            var competitorIdentity = generator.Generate(dto!.Ref);
            var teamIdentity = generator.Generate(dto.Team.Ref);

            var competitionId = Guid.NewGuid();
            await FootballDataContext.Competitions.AddAsync(new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = FixedTestNow,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });

            var franchiseSeasonId = Guid.NewGuid();
            await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
            {
                Id = franchiseSeasonId,
                Abbreviation = "TST",
                DisplayName = "Test FS",
                DisplayNameShort = "TFS",
                Slug = teamIdentity.CanonicalId.ToString(),
                Location = "Test Location",
                Name = "Test Franchise Season",
                ColorCodeHex = "#FFFFFF",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new FranchiseSeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = teamIdentity.CleanUrl,
                        SourceUrlHash = teamIdentity.UrlHash,
                        Value = teamIdentity.UrlHash
                    }
                ]
            });

            // The STALE occupant: a different franchise's row holding the
            // side the document claims (fixture: home). No row exists for
            // the document's own competitor.
            var occupantId = Guid.NewGuid();
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = occupantId,
                CompetitionId = competitionId,
                FranchiseSeasonId = Guid.NewGuid(),
                HomeAway = "home",
                Order = 0,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.ParentId, competitionId.ToString())
                .With(x => x.UrlHash, competitorIdentity.UrlHash)
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(command);

            var occupant = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == occupantId);
            occupant.HomeAway.Should().Be("away", "the stale occupant belongs on the vacated side");

            var created = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == competitorIdentity.CanonicalId);
            created.HomeAway.Should().Be("home");
        }

        [Fact]
        public async Task WhenBothCompetitorsSwapSides_ShouldSwapWithoutCollision()
        {
            // Full re-designation: OUR row holds away, the other franchise's
            // row holds home, and the document says we are now home. Both
            // rows must end swapped — the parking step keeps the relocation
            // from colliding with our own row.

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);
            var sut = Mocker.CreateInstance<FootballEventCompetitionCompetitorDocumentProcessor<FootballDataContext>>();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionCompetitor.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            var competitorIdentity = generator.Generate(dto!.Ref);
            var teamIdentity = generator.Generate(dto.Team.Ref);

            var competitionId = Guid.NewGuid();
            await FootballDataContext.Competitions.AddAsync(new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = FixedTestNow,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });

            var franchiseSeasonId = Guid.NewGuid();
            await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
            {
                Id = franchiseSeasonId,
                Abbreviation = "TST",
                DisplayName = "Test FS",
                DisplayNameShort = "TFS",
                Slug = teamIdentity.CanonicalId.ToString(),
                Location = "Test Location",
                Name = "Test Franchise Season",
                ColorCodeHex = "#FFFFFF",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new FranchiseSeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = teamIdentity.CleanUrl,
                        SourceUrlHash = teamIdentity.UrlHash,
                        Value = teamIdentity.UrlHash
                    }
                ]
            });

            // OUR row, keyed by the document's UrlHash, currently away.
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = competitorIdentity.CanonicalId,
                CompetitionId = competitionId,
                FranchiseSeasonId = franchiseSeasonId,
                HomeAway = "away",
                Order = 0,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new CompetitionCompetitorExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitorIdentity.CleanUrl,
                        SourceUrlHash = competitorIdentity.UrlHash,
                        Value = competitorIdentity.UrlHash
                    }
                ]
            });

            // The other franchise's row, currently home.
            var occupantId = Guid.NewGuid();
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = occupantId,
                CompetitionId = competitionId,
                FranchiseSeasonId = Guid.NewGuid(),
                HomeAway = "home",
                Order = 1,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.ParentId, competitionId.ToString())
                .With(x => x.UrlHash, competitorIdentity.UrlHash)
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(command);

            var ours = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == competitorIdentity.CanonicalId);
            ours.HomeAway.Should().Be("home");

            var occupant = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == occupantId);
            occupant.HomeAway.Should().Be("away");
        }

        [Fact]
        public void OrderParkingValue_IsNegative()
        {
            // Real orders are non-negative (0/1 or 1/2 per the index comment),
            // so the parking value must be negative to never collide under
            // the (CompetitionId, Order) unique index — which the InMemory
            // provider cannot enforce, hence the pin.
            EventCompetitionCompetitorDocumentProcessorBase<FootballDataContext>
                .OrderParkingValue.Should().BeNegative();
        }

        [Fact]
        public async Task WhenBothCompetitorsSwapOrders_ShouldRelocateOccupantOrder()
        {
            // ESPN re-designation flips Order together with HomeAway, but the
            // (CompetitionId, Order) unique index was never part of the swap
            // choreography — 2026-09-07 Wisconsin/Notre Dame at Lambeau: the
            // tail save collided on Order, stranding the parked row. Fixture
            // doc: home/0. OUR row holds home/1; the occupant holds away/0.
            // Both orders must end swapped.

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);
            var sut = Mocker.CreateInstance<FootballEventCompetitionCompetitorDocumentProcessor<FootballDataContext>>();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionCompetitor.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            var competitorIdentity = generator.Generate(dto!.Ref);
            var teamIdentity = generator.Generate(dto.Team.Ref);

            var competitionId = Guid.NewGuid();
            await FootballDataContext.Competitions.AddAsync(new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = FixedTestNow,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });

            var franchiseSeasonId = Guid.NewGuid();
            await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
            {
                Id = franchiseSeasonId,
                Abbreviation = "TST",
                DisplayName = "Test FS",
                DisplayNameShort = "TFS",
                Slug = teamIdentity.CanonicalId.ToString(),
                Location = "Test Location",
                Name = "Test Franchise Season",
                ColorCodeHex = "#FFFFFF",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new FranchiseSeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = teamIdentity.CleanUrl,
                        SourceUrlHash = teamIdentity.UrlHash,
                        Value = teamIdentity.UrlHash
                    }
                ]
            });

            // OUR row: correct side already, stale order (1 where doc says 0).
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = competitorIdentity.CanonicalId,
                CompetitionId = competitionId,
                FranchiseSeasonId = franchiseSeasonId,
                HomeAway = "home",
                Order = 1,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new CompetitionCompetitorExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitorIdentity.CleanUrl,
                        SourceUrlHash = competitorIdentity.UrlHash,
                        Value = competitorIdentity.UrlHash
                    }
                ]
            });

            // The other franchise's row holds the order our document claims.
            var occupantId = Guid.NewGuid();
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = occupantId,
                CompetitionId = competitionId,
                FranchiseSeasonId = Guid.NewGuid(),
                HomeAway = "away",
                Order = 0,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.ParentId, competitionId.ToString())
                .With(x => x.UrlHash, competitorIdentity.UrlHash)
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(command);

            var ours = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == competitorIdentity.CanonicalId);
            ours.Order.Should().Be(0);
            ours.HomeAway.Should().Be("home");

            var occupant = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == occupantId);
            occupant.Order.Should().Be(1, "the stale occupant takes the order we vacated");
        }

        [Fact]
        public async Task WhenOccupantStrandedAtSwapParking_ShouldStillResolveOrderAndConverge()
        {
            // The exact 2026-09-07 WI/ND production shape, from the failing
            // document's perspective: a prior crashed swap left the OTHER
            // franchise's row at HomeAway='swap' still holding Order 0; OUR
            // row is home/1; the doc says home/0. The side dance finds no
            // occupant (nobody holds 'home'), so only the Order dance can
            // free slot 0. End state: ours home/0; the stranded row keeps
            // 'swap' but moves to Order 1 (its own document heals its side).

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);
            var sut = Mocker.CreateInstance<FootballEventCompetitionCompetitorDocumentProcessor<FootballDataContext>>();

            var json = await LoadJsonTestData("EspnFootballNcaa/EspnFootballNcaaEventCompetitionCompetitor.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorDto>();

            var competitorIdentity = generator.Generate(dto!.Ref);
            var teamIdentity = generator.Generate(dto.Team.Ref);

            var competitionId = Guid.NewGuid();
            await FootballDataContext.Competitions.AddAsync(new FootballCompetition
            {
                Id = competitionId,
                ContestId = Guid.NewGuid(),
                Date = FixedTestNow,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });

            var franchiseSeasonId = Guid.NewGuid();
            await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
            {
                Id = franchiseSeasonId,
                Abbreviation = "TST",
                DisplayName = "Test FS",
                DisplayNameShort = "TFS",
                Slug = teamIdentity.CanonicalId.ToString(),
                Location = "Test Location",
                Name = "Test Franchise Season",
                ColorCodeHex = "#FFFFFF",
                ColorCodeAltHex = "#000000",
                IsActive = true,
                SeasonYear = 2024,
                FranchiseId = Guid.NewGuid(),
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new FranchiseSeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = teamIdentity.CleanUrl,
                        SourceUrlHash = teamIdentity.UrlHash,
                        Value = teamIdentity.UrlHash
                    }
                ]
            });

            // OUR row (Notre Dame in the incident): right side, stale order.
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = competitorIdentity.CanonicalId,
                CompetitionId = competitionId,
                FranchiseSeasonId = franchiseSeasonId,
                HomeAway = "home",
                Order = 1,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid(),
                ExternalIds =
                [
                    new CompetitionCompetitorExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = competitorIdentity.CleanUrl,
                        SourceUrlHash = competitorIdentity.UrlHash,
                        Value = competitorIdentity.UrlHash
                    }
                ]
            });

            // The stranded row (Wisconsin in the incident): parked side,
            // still holding the order our document claims.
            var strandedId = Guid.NewGuid();
            await FootballDataContext.CompetitionCompetitors.AddAsync(new FootballCompetitionCompetitor
            {
                Id = strandedId,
                CompetitionId = competitionId,
                FranchiseSeasonId = Guid.NewGuid(),
                HomeAway = EventCompetitionCompetitorDocumentProcessorBase<FootballDataContext>.SwapParkingValue,
                Order = 0,
                Winner = false,
                CreatedUtc = FixedTestNow,
                CreatedBy = Guid.NewGuid()
            });
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.Document, json)
                .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitor)
                .With(x => x.SeasonYear, 2024)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.ParentId, competitionId.ToString())
                .With(x => x.UrlHash, competitorIdentity.UrlHash)
                .OmitAutoProperties()
                .Create();

            await sut.ProcessAsync(command);

            var ours = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == competitorIdentity.CanonicalId);
            ours.HomeAway.Should().Be("home");
            ours.Order.Should().Be(0);

            var stranded = await FootballDataContext.CompetitionCompetitors
                .AsNoTracking()
                .FirstAsync(x => x.Id == strandedId);
            stranded.Order.Should().Be(1, "the stranded occupant vacates the order our document claims");
        }
    }
}
