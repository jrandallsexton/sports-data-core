
using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.TeamSports;

public class TeamSeasonRankDocumentProcessorTests :
    ProducerTestBase<TeamSeasonRankDocumentProcessor<FootballDataContext>>
{
    private const string SourceUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2019/types/2/weeks/16/teams/99/ranks/21";
    private readonly string _urlHash = SourceUrl.UrlHash();

    [Fact]
    public async Task AsEntity_ShouldCorrectlyMapJsonToRankingEntity()
    {
        // Arrange
        var json = await LoadJsonTestData("EspnFootballNcaaTeamSeasonRank.json");
        var dto = json.FromJson<EspnTeamSeasonRankDto>();

        var generator = new ExternalRefIdentityGenerator();
        var franchiseId = Guid.NewGuid();
        var franchiseSeasonId = Guid.NewGuid();
        var seasonYear = 2019;
        var correlationId = Guid.NewGuid();

        // Act
        var entity = dto.AsEntity(generator, franchiseId, franchiseSeasonId, seasonYear, correlationId);

        // Assert – Top-level ranking
        entity.Should().NotBeNull();
        entity.FranchiseId.Should().Be(franchiseId);
        entity.FranchiseSeasonId.Should().Be(franchiseSeasonId);
        entity.SeasonYear.Should().Be(seasonYear);

        entity.Name.Should().Be("Playoff Committee Rankings");
        entity.ShortName.Should().Be("CFP Rankings");
        entity.Type.Should().Be("cfp");
        entity.Headline.Should().Be("2019 NCAA Football Rankings - CFP Rankings Week 16");
        entity.ShortHeadline.Should().Be("2019 CFP Rankings: Week 16");
        entity.Date.Should().Be(DateTime.Parse("2019-12-08T08:00Z").ToUniversalTime());
        entity.LastUpdated.Should().Be(DateTime.Parse("2019-12-29T08:19Z").ToUniversalTime());
        entity.DefaultRanking.Should().BeFalse();

        // Assert – Occurrence
        entity.Occurrence.Should().NotBeNull();
        entity.Occurrence.Number.Should().Be(16);
        entity.Occurrence.Type.Should().Be("week");
        entity.Occurrence.Last.Should().BeFalse();
        entity.Occurrence.Value.Should().Be("16");
        entity.Occurrence.DisplayValue.Should().Be("Week 16");

        // Assert – Rank
        entity.Rank.Should().NotBeNull();
        entity.Rank.Current.Should().Be(1);
        entity.Rank.Previous.Should().Be(2);
        entity.Rank.Points.Should().Be(0.0);
        entity.Rank.FirstPlaceVotes.Should().Be(0);
        entity.Rank.Trend.Should().Be("+1");
        entity.Rank.Date.Should().Be(DateTime.Parse("2019-12-08T08:00Z").ToUniversalTime());
        entity.Rank.LastUpdated.Should().Be(DateTime.Parse("2019-12-29T08:19Z").ToUniversalTime());

        // Assert – Record
        entity.Rank.Record.Should().NotBeNull();
        entity.Rank.Record.Summary.Should().Be("13-0");
        entity.Rank.Record.Stats.Should().HaveCount(2);

        var wins = entity.Rank.Record.Stats.FirstOrDefault(s => s.Name == "wins");
        wins.Should().NotBeNull();
        wins!.DisplayName.Should().Be("Wins");
        wins.ShortDisplayName.Should().Be("W");
        wins.Description.Should().Be("Wins");
        wins.Abbreviation.Should().Be("W");
        wins.Type.Should().Be("wins");
        wins.Value.Should().Be(13.0);
        wins.DisplayValue.Should().Be("13");

        var losses = entity.Rank.Record.Stats.FirstOrDefault(s => s.Name == "losses");
        losses.Should().NotBeNull();
        losses!.DisplayName.Should().Be("Losses");
        losses.ShortDisplayName.Should().Be("L");
        losses.Description.Should().Be("Losses");
        losses.Abbreviation.Should().Be("L");
        losses.Type.Should().Be("losses");
        losses.Value.Should().Be(0.0);
        losses.DisplayValue.Should().Be("0");

        // Assert – Notes
        entity.Notes.Should().HaveCount(1);
        entity.Notes.First().Text.Should().Be("*: Receives First Round Bye");

        // Assert – External ID
        entity.ExternalIds.Should().ContainSingle();
        var externalId = entity.ExternalIds.First();
        externalId.Provider.Should().Be(SourceDataProvider.Espn);
        externalId.SourceUrl.Should().Be(SourceUrl);
        externalId.SourceUrlHash.Should().Be(SourceUrl.UrlHash());
    }

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
            .With(x => x.Seasons, new List<FranchiseSeason>())
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.SeasonYear, 2019)
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.Rankings, new List<FranchiseSeasonRanking>())
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
