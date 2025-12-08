#nullable enable

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

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// Tests for EventCompetitionPlayDocumentProcessor.
/// Optimized to eliminate AutoFixture overhead.
/// </summary>
[Collection("Sequential")]
public class EventCompetitionPlayDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    private const string PlayUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/plays/401628334123";

    private ProcessDocumentCommand CreateCommand(string jsonFile, string? parentId = null)
    {
        var generator = new ExternalRefIdentityGenerator();
        return Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.Document, jsonFile)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.Season, 2024)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.ParentId, parentId ?? Guid.NewGuid().ToString())
            .With(x => x.CorrelationId, Guid.NewGuid())
            .With(x => x.UrlHash, generator.Generate(PlayUrl).UrlHash)
            .Create();
    }

    [Fact]
    public async Task WhenPlayCollectionIsProvided_ScoreCanBeCalculate()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays.json");
        var plays = json.FromJson<List<EspnEventCompetitionPlayDto>>();

        // act
        var scoringPlays = plays!.Where(x => x.ScoringPlay).ToList();

        // assert
        scoringPlays.Count().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task WhenProcessingKickoffReturnPlay_ShouldCreatePlayWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);
        await FootballDataContext.SaveChangesAsync();

        // Setup team franchise seasons for both teams
        var startTeamId = Guid.NewGuid();
        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99";
        
        // OPTIMIZATION: Direct instantiation
        var startTeam = new FranchiseSeason
        {
            Id = startTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM1",
            DisplayName = "Team 1",
            DisplayNameShort = "T1",
            Location = "Location",
            Name = "Team 1",
            Slug = "team-1",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = startTeamId,
                    Provider = SourceDataProvider.Espn,
                    Value = generator.Generate(startTeamUrl).UrlHash,
                    SourceUrl = startTeamUrl,
                    SourceUrlHash = generator.Generate(startTeamUrl).UrlHash,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(startTeam);
        await FootballDataContext.SaveChangesAsync();

        var returnTeamId = Guid.NewGuid();
        var returnTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        
        // OPTIMIZATION: Direct instantiation
        var returnTeam = new FranchiseSeason
        {
            Id = returnTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM2",
            DisplayName = "Team 2",
            DisplayNameShort = "T2",
            Location = "Location",
            Name = "Team 2",
            Slug = "team-2",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = returnTeamId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = returnTeamUrl,
                    SourceUrlHash = generator.Generate(returnTeamUrl).UrlHash,
                    Value = generator.Generate(returnTeamUrl).UrlHash,
                    CreatedBy = Guid.NewGuid()
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(returnTeam);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Type.Should().Be(PlayType.KickoffReturnOffense);
        play.StatYardage.Should().Be(16);
        play.StartYardLine.Should().Be(65);
        play.EndYardLine.Should().Be(23);
        play.StartDown.Should().Be(1);
        play.StartFranchiseSeasonId.Should().Be(returnTeamId);
        play.EndFranchiseSeasonId.Should().Be(startTeamId);
        play.ClockValue.Should().Be(900);
        play.ClockDisplayValue.Should().Be("15:00");
        play.Text.Should().Be("Michael Lantz kickoff for 58 yds , Zavion Thomas return for 16 yds to the LSU 23");
    }

    [Fact]
    public async Task WhenEntityDoesNotExist_ShouldCreatePlayWithCorrectData()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var competition = new Competition
        {
            Id = competitionId,
            ContestId = Guid.NewGuid(),
            Date = DateTime.UtcNow,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.Competitions.AddAsync(competition);

        var startTeamId = Guid.NewGuid();
        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        
        // OPTIMIZATION: Direct instantiation
        var franchiseSeason = new FranchiseSeason
        {
            Id = startTeamId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "TEAM",
            DisplayName = "Team",
            DisplayNameShort = "T",
            Location = "Location",
            Name = "Team",
            Slug = "team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // OPTIMIZATION: Direct instantiation
        var externalId = new FranchiseSeasonExternalId
        {
            Id = Guid.NewGuid(),
            FranchiseSeasonId = startTeamId,
            Provider = SourceDataProvider.Espn,
            SourceUrl = startTeamUrl,
            SourceUrlHash = generator.Generate(startTeamUrl).UrlHash,
            Value = generator.Generate(startTeamUrl).UrlHash,
            CreatedBy = Guid.NewGuid()
        };
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(externalId);

        // Add the FranchiseSeason for dto.Team.Ref
        var teamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99?lang=en";
        var mainFranchiseSeasonId = Guid.NewGuid();
        
        // OPTIMIZATION: Direct instantiation
        var mainFranchiseSeason = new FranchiseSeason
        {
            Id = mainFranchiseSeasonId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2024,
            Abbreviation = "MAIN",
            DisplayName = "Main Team",
            DisplayNameShort = "MT",
            Location = "Location",
            Name = "Main Team",
            Slug = "main-team",
            ColorCodeHex = "#FFFFFF",
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            ExternalIds = new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    FranchiseSeasonId = mainFranchiseSeasonId,
                    Provider = SourceDataProvider.Espn,
                    SourceUrl = new Uri(teamUrl).ToCleanUrl(),
                    SourceUrlHash = generator.Generate(teamUrl).UrlHash,
                    Value = generator.Generate(teamUrl).UrlHash
                }
            }
        };

        await FootballDataContext.FranchiseSeasons.AddAsync(mainFranchiseSeason);

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Competition.Should().NotBeNull();
        play.Competition!.Id.Should().Be(competitionId);
    }

    [Fact(Skip="Updates not yet implemented")]
    public async Task WhenEntityExists_ShouldUpdateExistingPlay()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var competitionId = Guid.NewGuid();
        var competition = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, Guid.NewGuid())
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.Competitions.AddAsync(competition);

        var playId = Guid.NewGuid();
        var play = Fixture.Build<CompetitionPlay>()
            .With(x => x.Id, playId)
            .With(x => x.CompetitionId, competitionId)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.CompetitionPlays.AddAsync(play);

        var externalId = Fixture.Build<CompetitionPlayExternalId>()
            .With(x => x.CompetitionPlayId, playId)
            .With(x => x.Provider, SourceDataProvider.Espn)
            .With(x => x.SourceUrlHash, generator.Generate(PlayUrl).UrlHash)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        play.ExternalIds.Add(externalId);

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var updatedPlay = await FootballDataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == playId);

        updatedPlay.Should().NotBeNull();
        updatedPlay!.ExternalIds.Should().NotBeEmpty();
        updatedPlay.ModifiedBy.Should().NotBeNull();
    }

    [Fact(Skip = "log not throw")]
    public async Task WhenSeasonMissing_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            null, // Set Season to null
            command.DocumentType,
            command.Document,
            command.CorrelationId,
            command.ParentId,
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact(Skip="log not throw")]
    public async Task WhenParentIdInvalid_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            command.Season,
            command.DocumentType,
            command.Document,
            command.CorrelationId,
            "invalid-guid",
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }

    [Fact(Skip = "log not throw")]
    public async Task WhenParentIdMissing_ThrowsException()
    {
        // arrange
        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json);
        command = new ProcessDocumentCommand(
            command.SourceDataProvider,
            command.Sport,
            command.Season,
            command.DocumentType,
            command.Document,
            command.CorrelationId,
            null,
            command.SourceUri,
            command.UrlHash);

        // act & assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.ProcessAsync(command));
    }
}