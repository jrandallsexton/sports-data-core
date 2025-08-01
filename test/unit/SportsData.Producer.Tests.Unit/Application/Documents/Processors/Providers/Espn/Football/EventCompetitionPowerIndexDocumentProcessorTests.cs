using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    public class EventCompetitionPowerIndexDocumentProcessorTests :
        ProducerTestBase<EventCompetitionPowerIndexDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenCompetitionExists_IndexesAreAdded()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPowerIndex.json");

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.PowerIndexes, new List<CompetitionPowerIndex>())
                .Create();

            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .Create();

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
    }
}
