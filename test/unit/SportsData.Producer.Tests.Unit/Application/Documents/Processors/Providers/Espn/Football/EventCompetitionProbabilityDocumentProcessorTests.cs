﻿using AutoFixture;

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
    public class EventCompetitionProbabilityDocumentProcessorTests :
        ProducerTestBase<EventCompetitionProbabilityDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenCompetitionExists_ProbabilityIsAdded()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionProbability.json");

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Probabilities, new List<CompetitionProbability>())
                .With(x => x.ExternalIds, new List<CompetitionExternalId>())
                .Create();

            var competitionRef = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334?lang=en");

            competition.ExternalIds.Add(new CompetitionExternalId
            {
                Id = Guid.NewGuid(),
                CompetitionId = competition.Id,
                Provider = SourceDataProvider.Espn,
                SourceUrl = competitionRef.CleanUrl,
                SourceUrlHash = competitionRef.UrlHash,
                Value = competitionRef.UrlHash
            });

            await FootballDataContext.Competitions.AddAsync(competition);
            await FootballDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/probabilities/401628334101849901?lang=en".UrlHash())
                .With(x => x.DocumentType, DocumentType.EventCompetitionProbability)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.CorrelationId, Guid.NewGuid())
                .With(x => x.ParentId, competition.Id.ToString())
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionProbabilityDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var result = await FootballDataContext.CompetitionProbabilities
                .Where(x => x.CompetitionId == competition.Id)
                .ToListAsync();

            result.Should().NotBeEmpty();
        }
    }
}
