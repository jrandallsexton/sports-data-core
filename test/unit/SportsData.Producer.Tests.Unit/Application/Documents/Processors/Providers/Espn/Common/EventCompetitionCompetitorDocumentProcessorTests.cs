using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
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
                Date = DateTime.UtcNow,
                CreatedUtc = DateTime.UtcNow,
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
                CreatedUtc = DateTime.UtcNow,
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
                CreatedUtc = DateTime.UtcNow,
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
                .FirstAsync(x => x.Id == competitorIdentity.CanonicalId);
            saved.HomeAway.Should().Be("home");
            saved.Order.Should().Be(0);
            saved.Winner.Should().BeFalse();
        }
    }
}
