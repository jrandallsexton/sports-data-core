using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;
using Xunit;
using Xunit.Abstractions;
using FootballLeaderCategory = SportsData.Core.Common.FootballLeaderCategory;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class EventCompetitionLeadersDocumentProcessorTests : 
    ProducerTestBase<EventCompetitionLeadersDocumentProcessor<TeamSportDataContext>>
{
    private EspnEventCompetitionLeadersDto _dto;
    private readonly ITestOutputHelper _output;

    public EventCompetitionLeadersDocumentProcessorTests(ITestOutputHelper output)
    {
        _output = output;
        var documentJson = LoadJsonTestData("EspnFootballNcaaEventCompetitionLeaders.json").Result;
        _dto = documentJson.FromJson<EspnEventCompetitionLeadersDto>();
    }

    [Fact]
    public void ProcessDocument_ValidLeaderCategory_MapsCorrectly()
    {
        // Test the first category mapping
        var firstCategory = _dto.Categories.First();
        firstCategory.Name.Should().Be("passingLeader");
        firstCategory.DisplayName.Should().Be("Passing Leader");
        firstCategory.ShortDisplayName.Should().Be("PASS");
        firstCategory.Abbreviation.Should().Be("PYDS");

        var leaderCategory = FootballLeaderCategoryExtensions.FromName(firstCategory.Name);
        leaderCategory.Should().Be(FootballLeaderCategory.PassYards);
    }

    [Fact]
    public void ProcessDocument_RushingLeaderCategory_MapsCorrectly()
    {
        var category = _dto.Categories.First(c => c.Name == "rushingLeader");
        category.DisplayName.Should().Be("Rushing Leader");
        category.ShortDisplayName.Should().Be("RUSH");
        category.Abbreviation.Should().Be("RYDS");

        var leaderCategory = FootballLeaderCategoryExtensions.FromName(category.Name);
        leaderCategory.Should().Be(FootballLeaderCategory.RushYards);
    }

    [Fact]
    public void ProcessDocument_ReceivingLeaderCategory_MapsCorrectly()
    {
        var category = _dto.Categories.First(c => c.Name == "receivingLeader");
        category.DisplayName.Should().Be("Receiving Leader");
        category.ShortDisplayName.Should().Be("REC");
        category.Abbreviation.Should().Be("RECYDS");

        var leaderCategory = FootballLeaderCategoryExtensions.FromName(category.Name);
        leaderCategory.Should().Be(FootballLeaderCategory.RecYards);
    }

    [Fact]
    public void ProcessDocument_YardageCategories_MapCorrectly()
    {
        // Test passing yards
        var passYards = _dto.Categories.First(c => c.Name == "passingYards");
        passYards.DisplayName.Should().Be("Passing Yards");
        passYards.ShortDisplayName.Should().Be("PYDS");
        passYards.Abbreviation.Should().Be("YDS");
        FootballLeaderCategoryExtensions.FromName(passYards.Name).Should().Be(FootballLeaderCategory.PassingYards);

        // Test rushing yards
        var rushYards = _dto.Categories.First(c => c.Name == "rushingYards");
        rushYards.DisplayName.Should().Be("Rushing Yards");
        rushYards.ShortDisplayName.Should().Be("RYDS");
        rushYards.Abbreviation.Should().Be("YDS");
        FootballLeaderCategoryExtensions.FromName(rushYards.Name).Should().Be(FootballLeaderCategory.RushingYards);

        // Test receiving yards
        var recYards = _dto.Categories.First(c => c.Name == "receivingYards");
        recYards.DisplayName.Should().Be("Receiving Yards");
        recYards.ShortDisplayName.Should().Be("RECYDS");
        recYards.Abbreviation.Should().Be("YDS");
        FootballLeaderCategoryExtensions.FromName(recYards.Name).Should().Be(FootballLeaderCategory.ReceivingYards);
    }

    [Fact]
    public void ProcessDocument_TouchdownCategories_MapCorrectly()
    {
        // Test passing touchdowns
        var passTd = _dto.Categories.First(c => c.Name == "passingTouchdowns");
        passTd.DisplayName.Should().Be("Passing Touchdowns");
        passTd.ShortDisplayName.Should().Be("TD");
        passTd.Abbreviation.Should().Be("TD");
        FootballLeaderCategoryExtensions.FromName(passTd.Name).Should().Be(FootballLeaderCategory.PassTouchdowns);

        // Test rushing touchdowns
        var rushTd = _dto.Categories.First(c => c.Name == "rushingTouchdowns");
        rushTd.DisplayName.Should().Be("Rushing Touchdowns");
        rushTd.ShortDisplayName.Should().Be("TD");
        rushTd.Abbreviation.Should().Be("TD");
        FootballLeaderCategoryExtensions.FromName(rushTd.Name).Should().Be(FootballLeaderCategory.RushTouchdowns);

        // Test receiving touchdowns
        var recTd = _dto.Categories.First(c => c.Name == "receivingTouchdowns");
        recTd.DisplayName.Should().Be("Receiving Touchdowns");
        recTd.ShortDisplayName.Should().Be("TD");
        recTd.Abbreviation.Should().Be("TD");
        FootballLeaderCategoryExtensions.FromName(recTd.Name).Should().Be(FootballLeaderCategory.RecTouchdowns);
    }

    [Fact] 
    public void ProcessDocument_DefensiveCategories_MapCorrectly()
    {
        // Test tackles
        var tackles = _dto.Categories.First(c => c.Name == "totalTackles");
        tackles.DisplayName.Should().Be("Tackles");
        tackles.ShortDisplayName.Should().Be("TACK");
        tackles.Abbreviation.Should().Be("TOT");
        FootballLeaderCategoryExtensions.FromName(tackles.Name).Should().Be(FootballLeaderCategory.Tackles);

        // Test sacks
        var sacks = _dto.Categories.First(c => c.Name == "sacks");
        sacks.DisplayName.Should().Be("Sacks");
        sacks.ShortDisplayName.Should().Be("SACK");
        sacks.Abbreviation.Should().Be("SACK");
        FootballLeaderCategoryExtensions.FromName(sacks.Name).Should().Be(FootballLeaderCategory.Sacks);

        // Test interceptions
        var ints = _dto.Categories.First(c => c.Name == "interceptions");
        ints.DisplayName.Should().Be("Interceptions");
        ints.ShortDisplayName.Should().Be("INT");
        ints.Abbreviation.Should().Be("INT");
        FootballLeaderCategoryExtensions.FromName(ints.Name).Should().Be(FootballLeaderCategory.Interceptions);
    }

    [Fact]
    public void ProcessDocument_ReturnsAndPunts_MapCorrectly()
    {
        // Test punt returns
        var puntReturns = _dto.Categories.First(c => c.Name == "puntReturns");
        puntReturns.DisplayName.Should().Be("Punt Returns");
        puntReturns.ShortDisplayName.Should().Be("PR");
        puntReturns.Abbreviation.Should().Be("PR");
        FootballLeaderCategoryExtensions.FromName(puntReturns.Name).Should().Be(FootballLeaderCategory.PuntReturns);

        // Test kick returns
        var kickReturns = _dto.Categories.First(c => c.Name == "kickReturns");
        kickReturns.DisplayName.Should().Be("Kick Returns");
        kickReturns.ShortDisplayName.Should().Be("KR");
        kickReturns.Abbreviation.Should().Be("KR");
        FootballLeaderCategoryExtensions.FromName(kickReturns.Name).Should().Be(FootballLeaderCategory.KickReturns);

        // Test punts
        var punts = _dto.Categories.First(c => c.Name == "punts");
        punts.DisplayName.Should().Be("Punts");
        punts.ShortDisplayName.Should().Be("P");
        punts.Abbreviation.Should().Be("P");
        FootballLeaderCategoryExtensions.FromName(punts.Name).Should().Be(FootballLeaderCategory.Punts);
    }

    [Fact]
    public async Task WhenCompetitionExists_LeadersAreCreated()
    {
        // arrange
        var identityGenerator = new ExternalRefIdentityGenerator();
        var identity = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334?lang=en");

        var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionLeaders.json");

        var competition = Fixture.Build<Competition>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ExternalIds, new List<CompetitionExternalId>
            {
                    new CompetitionExternalId
                    {
                        Id = identity.CanonicalId,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash,
                        Value = identity.UrlHash
                    }
            })
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        // Also seed known LeaderCategory IDs
        FootballDataContext.LeaderCategories.AddRange(
            new CompetitionLeaderCategory
            {
                Id = 1,
                Name = "passingLeader",
                DisplayName = "Passing Leader",
                ShortDisplayName = "PASS",
                Abbreviation = "PYDS",
                CreatedUtc = DateTime.UtcNow
            });

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionLeadersDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/leaders?lang=en".UrlHash())
            .With(x => x.DocumentType, DocumentType.EventCompetitionLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert
        var leaderCount = await FootballDataContext.CompetitionLeaders.CountAsync();
        leaderCount.Should().BeGreaterThan(0);

        var statCount = await FootballDataContext.CompetitionLeaderStats.CountAsync();
        statCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WhenCompetitionExistsAndDataIsResolvable_LeadersAndStatsAreCreated()
    {
        // Arrange
        var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionLeaders.json");
        var leadersDto = documentJson.FromJson<EspnEventCompetitionLeadersDto>();
        var identityGenerator = new ExternalRefIdentityGenerator();
        var identity = identityGenerator.Generate("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334?lang=en");

        // Seed competition
        var competition = Fixture.Build<Competition>()
            .WithAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ExternalIds, new List<CompetitionExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash,
                    Value = identity.UrlHash
                }
            })
            .With(x => x.Leaders, new List<CompetitionLeader>())
            .Create();

        await FootballDataContext.Competitions.AddAsync(competition);

        // Seed LeaderCategories
        var nextCategoryId = 1;
        var expectedRecordCount = 0;
        foreach (var category in leadersDto.Categories)
        {
            FootballDataContext.LeaderCategories.Add(new CompetitionLeaderCategory
            {
                Id = nextCategoryId++,
                Name = category.Name,
                DisplayName = category.DisplayName,
                ShortDisplayName = category.ShortDisplayName,
                Abbreviation = category.Abbreviation,
                CreatedUtc = DateTime.UtcNow
            });
            expectedRecordCount += category.Leaders.Count;
        }

        // Seed Athlete + FranchiseSeason for each leader
        foreach (var leader in leadersDto.Categories.SelectMany(c => c.Leaders))
        {
            var athleteId = Guid.NewGuid();
            var teamId = Guid.NewGuid();

            var athleteHash = HashProvider.GenerateHashFromUri(leader.Athlete.Ref);
            var teamHash = HashProvider.GenerateHashFromUri(leader.Team.Ref);

            var athlete = Fixture.Build<FootballAthleteSeason>()
                .WithAutoProperties()
                .With(x => x.Id, athleteId)
                .With(x => x.ExternalIds, new List<AthleteSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        AthleteSeasonId = athleteId,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = leader.Athlete.Ref.ToString(),
                        SourceUrlHash = athleteHash,
                        Value = athleteHash
                    }
                })
                .Create();

            var franchiseSeason = Fixture.Build<FranchiseSeason>()
                .WithAutoProperties()
                .With(x => x.Id, teamId)
                .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        FranchiseSeasonId = teamId,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = leader.Team.Ref.ToString(),
                        SourceUrlHash = teamHash,
                        Value = teamHash
                    }
                })
                .Create();

            await FootballDataContext.AthleteSeasons.AddAsync(athlete);
            await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        }

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionLeadersDocumentProcessor<FootballDataContext>>();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .OmitAutoProperties()
            .With(x => x.Document, documentJson)
            .With(x => x.UrlHash, identity.UrlHash)
            .With(x => x.DocumentType, DocumentType.EventCompetitionLeaders)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.ParentId, competition.Id.ToString())
            .Create();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var leaders = await FootballDataContext.CompetitionLeaders
            .Include(x => x.Stats)
            .ToListAsync();

        var expected = leadersDto.Categories
            .Select(c => new
            {
                c.Name,
                Count = c.Leaders.Count
            })
            .OrderBy(c => c.Name);

        _output.WriteLine("Expected leader counts per category:");
        foreach (var cat in expected)
        {
            _output.WriteLine($"Category: {cat.Name} => Count: {cat.Count}");
        }

        var leaderCategories = await FootballDataContext.CompetitionLeaders.ToListAsync();
        leaderCategories.Should().HaveCount(leadersDto.Categories.Count);
    }
}