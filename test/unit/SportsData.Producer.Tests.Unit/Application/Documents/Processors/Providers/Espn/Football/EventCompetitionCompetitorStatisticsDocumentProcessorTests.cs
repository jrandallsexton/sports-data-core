using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football
{
    public class EventCompetitionCompetitorStatisticsDocumentProcessorTests
        : ProducerTestBase<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>
    {
        [Fact]
        public async Task ProcessAsync_Throws_WhenFranchiseSeasonNotFound()
        {
            // Arrange
            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .Create();

            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.SaveChangesAsync();

            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act & Assert
            var act = () => sut.ProcessAsync(command);
            await act.Should().ThrowAsync<InvalidOperationException>();
        }

        [Fact]
        public async Task ProcessAsync_Inserts_WhenValid()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorStatisticsDto>();
            
            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var identity = generator.Generate(dto.Team.Ref);

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Value = identity.UrlHash
                    }
                })
                .Create();

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var data = await TeamSportDataContext.CompetitionCompetitorStatistics
                .Include(x => x.Categories)
                .ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x =>
                    x.FranchiseSeasonId == franchiseSeason.Id &&
                    x.CompetitionId == competition.Id);

            data.Should().NotBeNull();
            data!.Categories.Should().NotBeEmpty();
            data.Categories.SelectMany(x => x.Stats).Should().NotBeEmpty();
        }

        [Fact]
        public async Task ProcessAsync_ReplacesExisting_WhenAlreadyPresent()
        {
            // Arrange
            var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorStatistics.json");
            var dto = json.FromJson<EspnEventCompetitionCompetitorStatisticsDto>();

            var generator = new ExternalRefIdentityGenerator();
            Mocker.Use<IGenerateExternalRefIdentities>(generator);

            var identity = generator.Generate(dto.Team.Ref);

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Value = identity.UrlHash
                    }
                })
                .Create();

            var competition = Fixture.Build<Competition>()
                .WithAutoProperties()
                .Create();

            var existing = Fixture.Build<CompetitionCompetitorStatistic>()
                .With(x => x.FranchiseSeasonId, franchiseSeason.Id)
                .With(x => x.CompetitionId, competition.Id)
                .With(x => x.Categories, new List<CompetitionCompetitorStatisticCategory>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Name = "OLD",
                        Stats = new List<CompetitionCompetitorStatisticStat>()
                    }
                })
                .Create();

            await TeamSportDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
            await TeamSportDataContext.Competitions.AddAsync(competition);
            await TeamSportDataContext.CompetitionCompetitorStatistics.AddAsync(existing);
            await TeamSportDataContext.SaveChangesAsync();

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.ParentId, competition.Id.ToString())
                .With(x => x.Document, json)
                .OmitAutoProperties()
                .Create();

            var sut = Mocker.CreateInstance<EventCompetitionCompetitorStatisticsDocumentProcessor<TeamSportDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var updated = await TeamSportDataContext.CompetitionCompetitorStatistics
                .Include(x => x.Categories)
                .ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x =>
                    x.FranchiseSeasonId == franchiseSeason.Id &&
                    x.CompetitionId == competition.Id);

            updated.Should().NotBeNull();
            updated!.Categories.Should().NotContain(c => c.Name == "OLD");
            updated.Categories.Should().NotBeEmpty();
            updated.Categories.SelectMany(x => x.Stats).Should().NotBeEmpty();
        }
    }
}
