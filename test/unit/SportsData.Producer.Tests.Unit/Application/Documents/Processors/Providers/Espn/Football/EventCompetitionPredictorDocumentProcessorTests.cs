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
    public class EventCompetitionPredictionDocumentProcessorTests :
        ProducerTestBase<EventCompetitionPredictionDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenCompetitionAndTeamsExist_PredictionsAreCreated()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPredictor.json");

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .Create();

            await FootballDataContext.Competitions.AddAsync(competition);

            var homeFranchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .Create();

            var awayFranchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .Create();

            var homeId = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en");
            var awayId = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30?lang=en");

            await FootballDataContext.FranchiseSeasons.AddRangeAsync(homeFranchiseSeason, awayFranchiseSeason);
            await FootballDataContext.FranchiseSeasonExternalIds.AddRangeAsync(
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = homeFranchiseSeason.Id,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = homeId.CleanUrl,
                    SourceUrlHash = homeId.UrlHash,
                    Value = homeId.UrlHash
                },
                new FranchiseSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = awayFranchiseSeason.Id,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = awayId.CleanUrl,
                    SourceUrlHash = awayId.UrlHash,
                    Value = awayId.UrlHash
                });

            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, documentJson)
                .With(x => x.DocumentType, DocumentType.EventCompetitionPrediction)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/predictor?lang=en".UrlHash())
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionPredictionDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var predictions = await FootballDataContext.CompetitionPredictions
                .Where(x => x.CompetitionId == competition.Id)
                .ToListAsync();

            predictions.Should().NotBeEmpty();
            predictions.Should().Contain(x => x.IsHome);
            predictions.Should().Contain(x => !x.IsHome);

            var metricCount = await FootballDataContext.PredictionMetrics.CountAsync();
            metricCount.Should().BeGreaterThan(0);
        }
    }
}
