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
    public class EventCompetitionStatusDocumentProcessorTests :
        ProducerTestBase<EventCompetitionStatusDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenCompetitionExists_StatusIsAdded()
        {
            // Arrange
            var identityGenerator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(identityGenerator);

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionStatus.json");

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .With(x => x.Id, Guid.NewGuid())
                .With(x => x.Status, (CompetitionStatus?)null)
                .With(x => x.ExternalIds, new List<CompetitionExternalId>())
                .Create();

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
    }
}
