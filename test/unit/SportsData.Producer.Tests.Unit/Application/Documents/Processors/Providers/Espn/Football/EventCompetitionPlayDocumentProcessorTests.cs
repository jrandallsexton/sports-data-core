#nullable enable

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

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

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
    public async Task WhenProcessingKickoffReturnPlay_ShouldCreatePlayWithCorrectData()
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

        // Setup team franchise seasons for both teams
        var startTeamId = Guid.NewGuid();
        var startTeam = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, startTeamId)
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.FranchiseSeasons.AddAsync(startTeam);

        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        var startTeamExternalId = Fixture.Build<FranchiseSeasonExternalId>()
            .With(x => x.FranchiseSeasonId, startTeamId)
            .With(x => x.Provider, SourceDataProvider.Espn)
            .With(x => x.SourceUrlHash, generator.Generate(startTeamUrl).UrlHash)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(startTeamExternalId);

        var returnTeamId = Guid.NewGuid();
        var returnTeam = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, returnTeamId)
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.FranchiseSeasons.AddAsync(returnTeam);

        var returnTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/99";
        var returnTeamExternalId = Fixture.Build<FranchiseSeasonExternalId>()
            .With(x => x.FranchiseSeasonId, returnTeamId)
            .With(x => x.Provider, SourceDataProvider.Espn)
            .With(x => x.SourceUrlHash, generator.Generate(returnTeamUrl).UrlHash)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(returnTeamExternalId);

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.Plays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Type.Should().Be(PlayType.KickoffReturnOffense);
        play.StatYardage.Should().Be(16);
        play.StartYardLine.Should().Be(65);
        play.EndYardLine.Should().Be(23);
        play.StartDown.Should().Be(1);
        play.StartTeamFranchiseSeasonId.Should().Be(startTeamId);
        play.TeamFranchiseSeasonId.Should().Be(returnTeamId);
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
        var competition = Fixture.Build<Competition>()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, Guid.NewGuid())
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.Competitions.AddAsync(competition);

        var startTeamId = Guid.NewGuid();
        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .With(x => x.Id, startTeamId)
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, 2024)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var startTeamUrl = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/teams/30";
        var externalId = Fixture.Build<FranchiseSeasonExternalId>()
            .With(x => x.FranchiseSeasonId, startTeamId)
            .With(x => x.Provider, SourceDataProvider.Espn)
            .With(x => x.SourceUrlHash, generator.Generate(startTeamUrl).UrlHash)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.FranchiseSeasonExternalIds.AddAsync(externalId);

        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<EventCompetitionPlayDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlay_KickoffReturnOffense.json");
        var command = CreateCommand(json, competitionId.ToString());

        // act
        await sut.ProcessAsync(command);

        // assert
        var play = await FootballDataContext.Plays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        play.Should().NotBeNull();
        play!.ExternalIds.Should().NotBeEmpty();
        play.Competition.Should().NotBeNull();
        play.Competition!.Id.Should().Be(competitionId);
    }

    [Fact]
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
        var play = Fixture.Build<Play>()
            .With(x => x.Id, playId)
            .With(x => x.CompetitionId, competitionId)
            .With(x => x.CreatedBy, Guid.NewGuid())
            .Create();
        await FootballDataContext.Plays.AddAsync(play);

        var externalId = Fixture.Build<PlayExternalId>()
            .With(x => x.PlayId, playId)
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
        var updatedPlay = await FootballDataContext.Plays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == playId);

        updatedPlay.Should().NotBeNull();
        updatedPlay!.ExternalIds.Should().NotBeEmpty();
        updatedPlay.ModifiedBy.Should().NotBeNull();
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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