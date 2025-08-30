using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class TeamSeasonRecordAtsDocumentProcessorTests :
    ProducerTestBase<TeamSeasonRecordAtsDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task WhenValidDocument_ShouldPersistAtsRecords()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<TeamSeasonRecordAtsDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonRecordAts.json");

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.RecordsAts, new List<FranchiseSeasonRecordAts>())
            .Create();

        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.TeamSeasonRecordAts)
            .With(x => x.Document, json)
            .With(x => x.ParentId, franchiseSeason.Id.ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var atsRecords = await FootballDataContext.FranchiseSeasonRecordsAts.ToListAsync();
        atsRecords.Should().NotBeEmpty();
        atsRecords.Should().HaveCount(8); // 8 records from the test JSON

        atsRecords.Select(r => r.FranchiseSeasonId).Distinct().Should().ContainSingle()
            .And.Contain(franchiseSeason.Id);
    }

    [Fact]
    public async Task WhenFranchiseSeasonNotFound_ShouldNotPersistAnything()
    {
        // Arrange
        var sut = Mocker.CreateInstance<TeamSeasonRecordAtsDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonRecordAts.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2024)
            .With(x => x.DocumentType, DocumentType.TeamSeasonRecordAts)
            .With(x => x.Document, json)
            .With(x => x.ParentId, Guid.NewGuid().ToString()) // Invalid/missing
            .With(x => x.CorrelationId, Guid.NewGuid())
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var count = await FootballDataContext.FranchiseSeasonRecordsAts.CountAsync();
        count.Should().Be(0);
    }
}
