
using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class TeamSeasonRankDocumentProcessorTests :
    ProducerTestBase<TeamSeasonRankDocumentProcessor<FootballDataContext>>
{
    private const string SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/ncaa/seasons/2019/types/2/weeks/16/teams/99/ranks/21";
    private readonly string _urlHash = SourceUrl.UrlHash();

    [Fact]
    public async Task WhenFranchiseSeasonExists_ShouldPersistRanking()
    {
        // Arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);
        var sut = Mocker.CreateInstance<TeamSeasonRankDocumentProcessor<FootballDataContext>>();
        var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonRank.json");

        var franchise = Fixture.Build<Franchise>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.SeasonYear, 2019)
            .With(x => x.FranchiseId, franchise.Id)
            .Create();

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2019)
            .With(x => x.DocumentType, DocumentType.TeamSeasonRank)
            .With(x => x.Document, json)
            .With(x => x.ParentId, franchiseSeason.Id.ToString())
            .With(x => x.UrlHash, _urlHash)
            .OmitAutoProperties()
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var rank = await FootballDataContext.FranchiseSeasonRankings.FirstOrDefaultAsync();
        rank.Should().NotBeNull();
        rank!.FranchiseSeasonId.Should().Be(franchiseSeason.Id);
        rank.SeasonYear.Should().Be(2019);
    }
}
