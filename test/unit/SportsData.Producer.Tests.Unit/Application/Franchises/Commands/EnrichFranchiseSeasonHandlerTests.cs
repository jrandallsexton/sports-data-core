using SportsData.Producer.Infrastructure.Data.Football.Entities;
using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Franchises.Commands;

/// <summary>
/// Unit tests for EnrichFranchiseSeasonHandler to verify win/loss calculations and scoring margins
/// </summary>
public class EnrichFranchiseSeasonHandlerTests :
    ProducerTestBase<EnrichFranchiseSeasonHandler<FootballDataContext>>
{
    [Fact]
    public async Task Process_ExcludesPreseasonContests_FromRecord()
    {
        // NFL preseason finals were counting into the W/L this enrichment
        // writes onto FranchiseSeason - the record every league matchup card
        // displays (prod 2026-09-09: Saints "1-2" after week 1). Preseason =
        // SeasonPhase TypeCode 1; a contest with no matching phase row stays
        // counted (NULL-phase-kept, same as the preview-history queries).
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2026;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);
        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddRangeAsync(franchise, opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(franchiseSeason, opponentSeason);

        var preseasonPhase = Fixture.Build<SeasonPhase>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TypeCode, 1)
            .With(x => x.Name, "Preseason")
            .With(x => x.Abbreviation, "PRE")
            .With(x => x.Slug, "preseason")
            .With(x => x.Year, seasonYear)
            .Create();
        await FootballDataContext.SeasonPhases.AddAsync(preseasonPhase);

        // Two preseason LOSSES that must NOT count
        var pre1 = CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 10, awayScore: 24, winnerId: opponentSeason.Id);
        pre1.SeasonPhaseId = preseasonPhase.Id;
        var pre2 = CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 31, awayScore: 17, winnerId: opponentSeason.Id);
        pre2.SeasonPhaseId = preseasonPhase.Id;
        await FootballDataContext.Contests.AddRangeAsync(pre1, pre2);

        // One regular-season win that must count — referencing a REAL
        // TypeCode-2 phase row, the only shape prod produces (the document
        // processor throws rather than persist a contest with an unsourced
        // phase). Without this, the keep-side was pinned only by an orphan
        // SeasonPhaseId and the filter could regress to "exclude anything
        // with a real phase" undetected (Vortex, PR #747).
        var regularPhase = Fixture.Build<SeasonPhase>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TypeCode, 2)
            .With(x => x.Name, "Regular Season")
            .With(x => x.Abbreviation, "REG")
            .With(x => x.Slug, "regular-season")
            .With(x => x.Year, seasonYear)
            .Create();
        await FootballDataContext.SeasonPhases.AddAsync(regularPhase);

        var regularWin = CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id);
        regularWin.SeasonPhaseId = regularPhase.Id;
        await FootballDataContext.Contests.AddAsync(regularWin);

        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(1, "only the regular-season win counts");
        enriched.Losses.Should().Be(0, "the two preseason losses are excluded");
    }

    [Fact]
    public async Task Process_CalculatesWinsAndLosses_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Create 3 wins, 2 losses
        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponentSeason);

        // Win 1
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Win 2
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 21, awayScore: 28, winnerId: franchiseSeason.Id));

        // Win 3
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 42, awayScore: 7, winnerId: franchiseSeason.Id));

        // Loss 1
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 10, awayScore: 24, winnerId: opponentSeason.Id));

        // Loss 2
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 31, awayScore: 17, winnerId: opponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(3, "there were 3 wins");
        enriched.Losses.Should().Be(2, "there were 2 losses");
        enriched.Ties.Should().Be(0, "there were no ties");
    }

    [Fact]
    public async Task Process_CalculatesTies_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponentSeason);

        // Win
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 28, awayScore: 14, winnerId: franchiseSeason.Id));

        // Tie (WinnerFranchiseSeasonId = null)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 21, awayScore: 21, winnerId: null));

        // Loss
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: opponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(1, "there was 1 win");
        enriched.Losses.Should().Be(1, "there was 1 loss");
        enriched.Ties.Should().Be(1, "there was 1 tie");
    }

    [Fact]
    public async Task Process_CalculatesConferenceWinsAndLosses_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var conferenceId = Guid.NewGuid();

        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear, conferenceId);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Same conference opponent
        var confOpponentFranchise = CreateFranchise();
        var confOpponentSeason = CreateFranchiseSeason(confOpponentFranchise.Id, seasonYear, conferenceId);
        await FootballDataContext.Franchises.AddAsync(confOpponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(confOpponentSeason);

        // Different conference opponent
        var nonConfOpponentFranchise = CreateFranchise();
        var nonConfOpponentSeason = CreateFranchiseSeason(nonConfOpponentFranchise.Id, seasonYear, Guid.NewGuid());
        await FootballDataContext.Franchises.AddAsync(nonConfOpponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(nonConfOpponentSeason);

        // Conference Win
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, confOpponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Conference Loss
        await FootballDataContext.Contests.AddAsync(CreateContest(
            confOpponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 28, awayScore: 21, winnerId: confOpponentSeason.Id));

        // Non-conference Win (should not count toward conference record)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, nonConfOpponentSeason.Id, seasonYear,
            homeScore: 42, awayScore: 7, winnerId: franchiseSeason.Id));

        // Non-conference Loss (should not count toward conference record)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            nonConfOpponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 31, awayScore: 10, winnerId: nonConfOpponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(2, "there were 2 total wins");
        enriched.Losses.Should().Be(2, "there were 2 total losses");
        enriched.ConferenceWins.Should().Be(1, "there was 1 conference win");
        enriched.ConferenceLosses.Should().Be(1, "there was 1 conference loss");
    }

    [Fact]
    public async Task Process_CalculatesConferenceTies_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var conferenceId = Guid.NewGuid();

        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear, conferenceId);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var confOpponentFranchise = CreateFranchise();
        var confOpponentSeason = CreateFranchiseSeason(confOpponentFranchise.Id, seasonYear, conferenceId);
        await FootballDataContext.Franchises.AddAsync(confOpponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(confOpponentSeason);

        // Conference Win
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, confOpponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Conference Tie (WinnerFranchiseSeasonId = null)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, confOpponentSeason.Id, seasonYear,
            homeScore: 21, awayScore: 21, winnerId: null));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(1, "there was 1 win");
        enriched.Losses.Should().Be(0, "there were no losses");
        enriched.Ties.Should().Be(1, "there was 1 tie");
        enriched.ConferenceWins.Should().Be(1, "there was 1 conference win");
        enriched.ConferenceLosses.Should().Be(0, "there were no conference losses");
        enriched.ConferenceTies.Should().Be(1, "there was 1 conference tie");

        // Ties must not contribute to loss-margin statistics
        enriched.MarginLossMin.Should().BeNull("ties should not populate loss margin fields");
        enriched.MarginLossMax.Should().BeNull("ties should not populate loss margin fields");
        enriched.MarginLossAvg.Should().BeNull("ties should not populate loss margin fields");
    }

    [Fact]
    public async Task Process_ConferenceRecord_CountsGamesAcrossDivisions()
    {
        // Teams sit in division groups (SEC - West, IsConference false) under
        // the conference (Southeastern Conference, IsConference true). A
        // conference record covers the whole conference: Auburn 2010 played
        // 8 SEC games but read 5-0, counting only SEC West opponents.
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2010;
        var fbs = CreateGroupSeason("FBS", isConference: false, parentId: null, seasonYear);
        var sec = CreateGroupSeason("Southeastern Conference", isConference: true, parentId: fbs.Id, seasonYear);
        var secWest = CreateGroupSeason("SEC - West", isConference: false, parentId: sec.Id, seasonYear);
        var secEast = CreateGroupSeason("SEC - East", isConference: false, parentId: sec.Id, seasonYear);
        var acc = CreateGroupSeason("Atlantic Coast Conference", isConference: true, parentId: fbs.Id, seasonYear);
        var accAtlantic = CreateGroupSeason("ACC - Atlantic", isConference: false, parentId: acc.Id, seasonYear);
        await FootballDataContext.GroupSeasons.AddRangeAsync(fbs, sec, secWest, secEast, acc, accAtlantic);

        var team = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secWest.Id);
        var sameDivision = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secWest.Id);
        var otherDivision = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secEast.Id);
        var otherConference = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, accAtlantic.Id);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(team, sameDivision, otherDivision, otherConference);

        await FootballDataContext.Contests.AddRangeAsync(
            CreateContest(team.Id, sameDivision.Id, seasonYear, 28, 10, team.Id),
            CreateContest(otherDivision.Id, team.Id, seasonYear, 17, 24, team.Id),
            CreateContest(team.Id, otherDivision.Id, seasonYear, 14, 21, otherDivision.Id),
            CreateContest(team.Id, otherConference.Id, seasonYear, 35, 7, team.Id));
        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(team.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons.FirstAsync(fs => fs.Id == team.Id);

        enriched.Wins.Should().Be(3);
        enriched.Losses.Should().Be(1);
        enriched.ConferenceWins.Should().Be(2, "the SEC West and SEC East wins are both conference games");
        enriched.ConferenceLosses.Should().Be(1, "the SEC East loss is a conference game");
        enriched.ConferenceTies.Should().Be(0);
    }

    [Fact]
    public async Task Process_ConferenceRecord_DivisionsWithoutConferenceParent_CompareOnOwnGroup()
    {
        // Some division groups have no conference above them (Northern Sun -
        // North/South have no parent row). With no conference to roll up to,
        // each compares on its own group, as before.
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2015;
        var north = CreateGroupSeason("Northern Sun - North", isConference: false, parentId: null, seasonYear);
        var south = CreateGroupSeason("Northern Sun - South", isConference: false, parentId: null, seasonYear);
        await FootballDataContext.GroupSeasons.AddRangeAsync(north, south);

        var team = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, north.Id);
        var sameGroup = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, north.Id);
        var otherGroup = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, south.Id);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(team, sameGroup, otherGroup);

        await FootballDataContext.Contests.AddRangeAsync(
            CreateContest(team.Id, sameGroup.Id, seasonYear, 28, 10, team.Id),
            CreateContest(team.Id, otherGroup.Id, seasonYear, 28, 10, team.Id));
        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(team.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons.FirstAsync(fs => fs.Id == team.Id);

        enriched.Wins.Should().Be(2);
        enriched.ConferenceWins.Should().Be(1, "only the same-group opponent counts");
    }

    [Fact]
    public async Task Process_ConferenceRecord_WhenSeasonHasConferenceFlags_UsesTheFlag()
    {
        // ESPN publishes conferenceCompetition from 2014 on and it matches the
        // official conference record: the title game is a same-conference game
        // ESPN does not flag, and a flagged game counts even when the two
        // teams' groups disagree (membership data can be wrong).
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2015;
        var sec = CreateGroupSeason("Southeastern Conference", isConference: true, parentId: null, seasonYear);
        var secWest = CreateGroupSeason("SEC - West", isConference: false, parentId: sec.Id, seasonYear);
        var secEast = CreateGroupSeason("SEC - East", isConference: false, parentId: sec.Id, seasonYear);
        var other = CreateGroupSeason("Mid-American Conference", isConference: true, parentId: null, seasonYear);
        await FootballDataContext.GroupSeasons.AddRangeAsync(sec, secWest, secEast, other);

        var team = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secWest.Id);
        var conferenceOpponent = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secEast.Id);
        var misassignedOpponent = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, other.Id);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(team, conferenceOpponent, misassignedOpponent);

        var flaggedWin = CreateContest(team.Id, conferenceOpponent.Id, seasonYear, 28, 10, team.Id);
        var titleGameWin = CreateContest(team.Id, conferenceOpponent.Id, seasonYear, 29, 15, team.Id);
        var flaggedLossVsMisassigned = CreateContest(misassignedOpponent.Id, team.Id, seasonYear, 24, 17, misassignedOpponent.Id);
        await FootballDataContext.Contests.AddRangeAsync(flaggedWin, titleGameWin, flaggedLossVsMisassigned);
        await FootballDataContext.Competitions.AddRangeAsync(
            CreateCompetition(flaggedWin.Id, isConferenceCompetition: true),
            CreateCompetition(titleGameWin.Id, isConferenceCompetition: false, noteHeadline: "SEC Championship Game"),
            CreateCompetition(flaggedLossVsMisassigned.Id, isConferenceCompetition: true));
        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(team.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons.FirstAsync(fs => fs.Id == team.Id);

        enriched.Wins.Should().Be(2);
        enriched.Losses.Should().Be(1);
        enriched.ConferenceWins.Should().Be(1, "the unflagged title game is not a conference game");
        enriched.ConferenceLosses.Should().Be(1, "the flagged game counts whatever the groups say");
    }

    [Fact]
    public async Task Process_ConferenceRecord_WithoutConferenceFlags_ExcludesChampionshipAndPostseasonGames()
    {
        // Before 2014 (and in NFL) no game carries the flag, so the record
        // compares conferences. The conference title game is a regular-season
        // game between two conference members; its note marks it. Postseason
        // games between members (an all-SEC title game, AFC playoffs) are not
        // conference games either.
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2010;
        var sec = CreateGroupSeason("Southeastern Conference", isConference: true, parentId: null, seasonYear);
        var secWest = CreateGroupSeason("SEC - West", isConference: false, parentId: sec.Id, seasonYear);
        var secEast = CreateGroupSeason("SEC - East", isConference: false, parentId: sec.Id, seasonYear);
        await FootballDataContext.GroupSeasons.AddRangeAsync(sec, secWest, secEast);

        var postseasonPhase = Fixture.Build<SeasonPhase>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.TypeCode, 3)
            .With(x => x.Name, "Postseason")
            .With(x => x.Abbreviation, "POST")
            .With(x => x.Slug, "postseason")
            .With(x => x.Year, seasonYear)
            .Create();
        await FootballDataContext.SeasonPhases.AddAsync(postseasonPhase);

        var team = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secWest.Id);
        var opponent = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, secEast.Id);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(team, opponent);

        var regularSeasonWin = CreateContest(opponent.Id, team.Id, seasonYear, 27, 35, team.Id);
        var titleGameWin = CreateContest(opponent.Id, team.Id, seasonYear, 17, 56, team.Id);
        var postseasonLoss = CreateContest(team.Id, opponent.Id, seasonYear, 0, 21, opponent.Id);
        postseasonLoss.SeasonPhaseId = postseasonPhase.Id;
        await FootballDataContext.Contests.AddRangeAsync(regularSeasonWin, titleGameWin, postseasonLoss);
        await FootballDataContext.Competitions.AddRangeAsync(
            CreateCompetition(regularSeasonWin.Id, isConferenceCompetition: false),
            CreateCompetition(titleGameWin.Id, isConferenceCompetition: false, noteHeadline: "SEC CHAMPIONSHIP GAME"),
            CreateCompetition(postseasonLoss.Id, isConferenceCompetition: false));
        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(team.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons.FirstAsync(fs => fs.Id == team.Id);

        enriched.Wins.Should().Be(2);
        enriched.Losses.Should().Be(1);
        enriched.ConferenceWins.Should().Be(1, "only the regular-season game counts; the title game is noted as a championship");
        enriched.ConferenceLosses.Should().Be(0, "the postseason game is not a conference game");
    }

    [Fact]
    public async Task Process_ConferenceRecord_WhenSeasonHasOnlyStrayFlags_ComparesConferences()
    {
        // NCAAFB 2008 has 2 flagged games of ~3,500. A season that stray must
        // not switch to the flag, or every unflagged team reads 0-0.
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2008;
        var sec = CreateGroupSeason("Southeastern Conference", isConference: true, parentId: null, seasonYear);
        await FootballDataContext.GroupSeasons.AddAsync(sec);

        var team = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, sec.Id);
        var opponent = CreateFranchiseSeason(CreateFranchise().Id, seasonYear, sec.Id);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(team, opponent);

        var conferenceWin = CreateContest(team.Id, opponent.Id, seasonYear, 28, 10, team.Id);
        await FootballDataContext.Contests.AddAsync(conferenceWin);
        await FootballDataContext.Competitions.AddAsync(CreateCompetition(conferenceWin.Id, isConferenceCompetition: false));

        // 1 flagged game among 11 in the season
        for (var i = 0; i < 10; i++)
        {
            var unrelated = CreateContest(Guid.NewGuid(), Guid.NewGuid(), seasonYear, 14, 7, null);
            await FootballDataContext.Contests.AddAsync(unrelated);
            await FootballDataContext.Competitions.AddAsync(CreateCompetition(unrelated.Id, isConferenceCompetition: i == 0));
        }

        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(team.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons.FirstAsync(fs => fs.Id == team.Id);

        enriched.ConferenceWins.Should().Be(1, "a stray flag does not switch the season to the flag");
    }

    [Fact]
    public async Task Process_WhenContestHasTwoCompetitions_StillEnriches()
    {
        // Contest:Competition is 1:1 in practice but not enforced; a second
        // row must not abort the enrichment.
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2015;
        var team = CreateFranchiseSeason(CreateFranchise().Id, seasonYear);
        var opponent = CreateFranchiseSeason(CreateFranchise().Id, seasonYear);
        await FootballDataContext.FranchiseSeasons.AddRangeAsync(team, opponent);

        var win = CreateContest(team.Id, opponent.Id, seasonYear, 28, 10, team.Id);
        await FootballDataContext.Contests.AddAsync(win);
        await FootballDataContext.Competitions.AddRangeAsync(
            CreateCompetition(win.Id, isConferenceCompetition: true),
            CreateCompetition(win.Id, isConferenceCompetition: true));
        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(team.Id, seasonYear, Guid.NewGuid()));

        var enriched = await FootballDataContext.FranchiseSeasons.FirstAsync(fs => fs.Id == team.Id);

        enriched.Wins.Should().Be(1);
        enriched.ConferenceWins.Should().Be(1);
    }

    [Fact]
    public async Task Process_CalculatesScoringMargins_Correctly()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        var opponentFranchise = CreateFranchise();
        var opponentSeason = CreateFranchiseSeason(opponentFranchise.Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(opponentFranchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponentSeason);

        // Win by 21: 35-14 (scored 35, allowed 14)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 35, awayScore: 14, winnerId: franchiseSeason.Id));

        // Win by 7: 28-21 (scored 28, allowed 21)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            opponentSeason.Id, franchiseSeason.Id, seasonYear,
            homeScore: 21, awayScore: 28, winnerId: franchiseSeason.Id));

        // Loss by 14: 10-24 (scored 10, allowed 24)
        await FootballDataContext.Contests.AddAsync(CreateContest(
            franchiseSeason.Id, opponentSeason.Id, seasonYear,
            homeScore: 10, awayScore: 24, winnerId: opponentSeason.Id));

        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        // Points Scored: 35, 28, 10
        enriched.PtsScoredMin.Should().Be(10);
        enriched.PtsScoredMax.Should().Be(35);
        enriched.PtsScoredAvg.Should().Be(24.33m); // (35+28+10)/3 = 24.33

        // Points Allowed: 14, 21, 24
        enriched.PtsAllowedMin.Should().Be(14);
        enriched.PtsAllowedMax.Should().Be(24);
        enriched.PtsAllowedAvg.Should().Be(19.67m); // (14+21+24)/3 = 19.67

        // Win Margins: 21, 7
        enriched.MarginWinMin.Should().Be(7);
        enriched.MarginWinMax.Should().Be(21);
        enriched.MarginWinAvg.Should().Be(14m); // (21+7)/2 = 14

        // Loss Margins: 14 (absolute value)
        enriched.MarginLossMin.Should().Be(14);
        enriched.MarginLossMax.Should().Be(14);
        enriched.MarginLossAvg.Should().Be(14m);
    }

    [Fact]
    public async Task Process_PublishesFranchiseSeasonEnrichmentCompleted_Event()
    {
        // Arrange
        var eventBus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var correlationId = Guid.NewGuid();
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            correlationId);

        // Act
        await sut.Process(command);

        // Assert
        eventBus.Verify(
            x => x.Publish(
                It.Is<FranchiseSeasonEnrichmentCompleted>(e =>
                    e.FranchiseSeasonId == franchiseSeason.Id &&
                    e.SeasonYear == seasonYear &&
                    e.CorrelationId == correlationId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_PublishedEvent_NamesEveryContestForTheTeam_PlayedOrNot()
    {
        // The API's league cards show this team's record on games NOT yet
        // played; the event must name those too, or the consumer cannot find
        // the rows to correct. Finalized-only would miss exactly the cards
        // that matter this week.
        var eventBus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);
        var opponent = CreateFranchiseSeason(CreateFranchise().Id, seasonYear);
        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.FranchiseSeasons.AddAsync(opponent);

        var played = CreateContest(franchiseSeason.Id, opponent.Id, seasonYear, 21, 14, franchiseSeason.Id);
        var upcoming = CreateContest(opponent.Id, franchiseSeason.Id, seasonYear, 0, 0, null);
        upcoming.FinalizedUtc = null;
        var unrelated = CreateContest(opponent.Id, Guid.NewGuid(), seasonYear, 0, 0, null);
        unrelated.FinalizedUtc = null;
        await FootballDataContext.Contests.AddRangeAsync(played, upcoming, unrelated);
        await FootballDataContext.SaveChangesAsync();

        await sut.Process(new EnrichFranchiseSeasonCommand(franchiseSeason.Id, seasonYear, Guid.NewGuid()));

        eventBus.Verify(
            x => x.Publish(
                It.Is<FranchiseSeasonEnrichmentCompleted>(e =>
                    e.ContestIds != null &&
                    e.ContestIds.Count == 2 &&
                    e.ContestIds.Contains(played.Id) &&
                    e.ContestIds.Contains(upcoming.Id) &&
                    !e.ContestIds.Contains(unrelated.Id)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Process_WhenNoContests_SetsRecordToZero()
    {
        // Arrange
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonHandler<FootballDataContext>>();

        var seasonYear = 2024;
        var franchise = CreateFranchise();
        var franchiseSeason = CreateFranchiseSeason(franchise.Id, seasonYear);

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);
        await FootballDataContext.SaveChangesAsync();

        var command = new EnrichFranchiseSeasonCommand(
            franchiseSeason.Id,
            seasonYear,
            Guid.NewGuid());

        // Act
        await sut.Process(command);

        // Assert
        var enriched = await FootballDataContext.FranchiseSeasons
            .FirstAsync(fs => fs.Id == franchiseSeason.Id);

        enriched.Wins.Should().Be(0);
        enriched.Losses.Should().Be(0);
        enriched.Ties.Should().Be(0);
        enriched.PtsScoredMin.Should().BeNull();
        enriched.PtsScoredMax.Should().BeNull();
        enriched.PtsScoredAvg.Should().BeNull();
    }

    private Franchise CreateFranchise()
    {
        return Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Franchise")
            .With(x => x.DisplayName, "Test Franchise")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Sport, Sport.FootballNcaa)
            .Create();
    }

    private GroupSeason CreateGroupSeason(string name, bool isConference, Guid? parentId, int seasonYear)
    {
        return Fixture.Build<GroupSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ParentId, parentId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Name, name)
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.Abbreviation, name)
            .With(x => x.IsConference, isConference)
            .Create();
    }

    private FootballCompetition CreateCompetition(Guid contestId, bool isConferenceCompetition, string noteHeadline = null)
    {
        var competitionId = Guid.NewGuid();

        return Fixture.Build<FootballCompetition>()
            .OmitAutoProperties()
            .With(x => x.Id, competitionId)
            .With(x => x.ContestId, contestId)
            .With(x => x.IsConferenceCompetition, isConferenceCompetition)
            .With(x => x.Notes, noteHeadline is null
                ? new List<CompetitionNote>()
                : new List<CompetitionNote>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Type = "event",
                        Headline = noteHeadline,
                        CompetitionId = competitionId
                    }
                })
            .Create();
    }

    private FranchiseSeason CreateFranchiseSeason(Guid franchiseId, int seasonYear, Guid? groupSeasonId = null)
    {
        return Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchiseId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.GroupSeasonId, groupSeasonId)
            .With(x => x.Name, "Test Season")
            .With(x => x.DisplayName, "Test Season")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Abbreviation, "TEST")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.ExternalIds, new List<FranchiseSeasonExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = "test-value",
                    SourceUrl = "http://example.com/test",
                    SourceUrlHash = "test-hash"
                }
            })
            .Create();
    }

    private FootballContest CreateContest(
        Guid homeTeamId,
        Guid awayTeamId,
        int seasonYear,
        int homeScore,
        int awayScore,
        Guid? winnerId)
    {
        return Fixture.Build<FootballContest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.HomeTeamFranchiseSeasonId, homeTeamId)
            .With(x => x.AwayTeamFranchiseSeasonId, awayTeamId)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.HomeScore, homeScore)
            .With(x => x.AwayScore, awayScore)
            .With(x => x.WinnerFranchiseSeasonId, winnerId)
            .With(x => x.FinalizedUtc, DateTime.UtcNow)
            .With(x => x.Name, "Test Contest")
            .With(x => x.ShortName, "Test")
            .With(x => x.Sport, Sport.FootballNcaa)
            .Create();
    }
}
