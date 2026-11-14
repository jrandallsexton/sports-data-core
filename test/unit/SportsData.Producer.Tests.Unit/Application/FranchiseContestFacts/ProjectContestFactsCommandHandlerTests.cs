using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.FranchiseContestFacts.Commands.ProjectContestFacts;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseContestFacts;

/// <summary>
/// Loading and upsert around the projector (the derivation rules themselves
/// are covered in <see cref="FranchiseContestFactProjectorTests"/>).
/// </summary>
public class ProjectContestFactsCommandHandlerTests : ProducerTestBase<ProjectContestFactsCommandHandler>
{
    private const int SeasonYear = 2025;
    private static readonly DateTime Kickoff = new(2025, 11, 1, 23, 30, 0, DateTimeKind.Utc);

    public ProjectContestFactsCommandHandlerTests()
    {
        Mocker.GetMock<IAppMode>().Setup(x => x.CurrentSport).Returns(Sport.FootballNcaa);
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow())
            .Returns(new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task Execute_ProjectsBothSides_FromContestCompetitionOddsRanksBoxAndVenue()
    {
        var home = SeedTeam("NCAAF|NCAA|fbs|sec");
        var away = SeedTeam("NCAAF|NCAA|fbs|sec");
        var venue = SeedVenue("America/Chicago");
        var contest = SeedContest(home, away, Kickoff, homeScore: 28, awayScore: 24, winner: home, spreadWinner: away, venue: venue);
        var competitionId = SeedCompetition(contest.Id, timeValid: true);
        SeedCompetitor(competitionId, home, "home", rank: 9);
        SeedCompetitor(competitionId, away, "away", rank: 99);
        SeedOdds(competitionId, providerId: "58", homeSpread: -6.5m, overUnder: 51.5m);
        SeedBox(competitionId, home, totalYards: 412, sacks: 2);

        // A prior home win and a prior away loss, plus a later game that must not count.
        SeedContest(home, SeedTeam("NCAAF|NCAA|fcs|caa"), Kickoff.AddDays(-7), 35, 10, winner: home);
        var other = SeedTeam("NCAAF|NCAA|fbs|acc");
        SeedContest(other, away, Kickoff.AddDays(-7), 21, 14, winner: other);
        SeedContest(home, other, Kickoff.AddDays(7), 10, 3, winner: home);
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<ProjectContestFactsCommandHandler>()
            .ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);

        var facts = await FootballDataContext.FranchiseContestFacts.AsNoTracking()
            .Where(f => f.ContestId == contest.Id).ToDictionaryAsync(f => f.FranchiseSeasonId);
        facts.Should().HaveCount(2);

        var h = facts[home.Id];
        h.Site.Should().Be(GameSite.Home);
        h.Result.Should().Be(GameResult.Win);
        h.Spread.Should().Be(-6.5m);
        h.OverUnder.Should().Be(51.5m);
        h.AtsResult.Should().Be(AtsResult.NotCovered, "won by 4 as a 6.5-point favorite");
        h.RankGoingIn.Should().Be(9);
        h.OpponentRankGoingIn.Should().Be(99);
        h.WinsBefore.Should().Be(1);
        h.LossesBefore.Should().Be(0);
        h.OpponentLossesBefore.Should().Be(1);
        h.TotalYards.Should().Be(412);
        h.SacksMade.Should().Be(2);
        h.KickoffLocal.Should().Be(new DateTime(2025, 11, 1, 18, 30, 0), "18:30 CDT");
        h.KickoffWindow.Should().Be(KickoffWindow.Night);
        h.Conference.Should().Be("sec");

        var a = facts[away.Id];
        a.Site.Should().Be(GameSite.Away);
        a.AtsResult.Should().Be(AtsResult.Covered);
        a.Spread.Should().Be(6.5m);
        a.TotalYards.Should().BeNull("the away team has no box score");
    }

    [Fact]
    public async Task Execute_UsesASuppliedSeasonFlag_InsteadOfCountingTheSeason()
    {
        // The seeded season has no flagged competitions, so a count would say
        // "season does not use the flag" and fall back to shared conference
        // (none here). The rebuild's precomputed true must win.
        var home = SeedTeam("NCAAF|NCAA|fbs|sec");
        var away = SeedTeam("NCAAF|NCAA|fbs|acc");
        var contest = SeedContest(home, away, Kickoff, 28, 24, winner: home);
        var competition = Fixture.Build<FootballCompetition>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, contest.Id)
            .With(x => x.TimeValid, true)
            .With(x => x.IsConferenceCompetition, true)
            .With(x => x.Notes, new List<CompetitionNote>())
            .Create();
        FootballDataContext.Competitions.Add(competition);
        // Pad the season with unflagged games so a recount would fall under 10%.
        for (var i = 0; i < 20; i++)
        {
            var filler = SeedContest(SeedTeam("NCAAF|NCAA|fbs|sec"), SeedTeam("NCAAF|NCAA|fbs|sec"), Kickoff.AddDays(-30), 7, 3);
            SeedCompetition(filler.Id, timeValid: true);
        }
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ProjectContestFactsCommandHandler>();

        await sut.ExecuteAsync(new ProjectContestFactsCommand(contest.Id));
        (await FootballDataContext.FranchiseContestFacts.AsNoTracking()
                .Where(f => f.ContestId == contest.Id).ToListAsync())
            .Should().OnlyContain(f => !f.IsConferenceGame, "a recount says the season does not use the flag");

        await sut.ExecuteAsync(new ProjectContestFactsCommand(contest.Id, SeasonUsesConferenceFlag: true));
        (await FootballDataContext.FranchiseContestFacts.AsNoTracking()
                .Where(f => f.ContestId == contest.Id).ToListAsync())
            .Should().OnlyContain(f => f.IsConferenceGame, "the supplied flag is used as given");
    }

    [Fact]
    public async Task Execute_OnNflPod_StoresNoRank()
    {
        // ESPN gives every NFL competitor 99; that is not a poll position.
        Mocker.GetMock<IAppMode>().Setup(x => x.CurrentSport).Returns(Sport.FootballNfl);
        var home = SeedTeam("NFL|NFC|NORTH");
        var away = SeedTeam("NFL|AFC|EAST");
        var contest = SeedContest(home, away, Kickoff, 6, 23, winner: away);
        var competitionId = SeedCompetition(contest.Id, timeValid: true);
        SeedCompetitor(competitionId, home, "home", rank: 99);
        SeedCompetitor(competitionId, away, "away", rank: 99);
        await FootballDataContext.SaveChangesAsync();

        await Mocker.CreateInstance<ProjectContestFactsCommandHandler>()
            .ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        var facts = await FootballDataContext.FranchiseContestFacts.AsNoTracking()
            .Where(f => f.ContestId == contest.Id).ToListAsync();
        facts.Should().HaveCount(2, "the FBS scope applies to NCAAFB only");
        facts.Should().OnlyContain(f => f.RankGoingIn == null && f.OpponentRankGoingIn == null);
    }

    [Fact]
    public async Task Execute_IsIdempotent_AndUpdatesInPlace()
    {
        var home = SeedTeam("NCAAF|NCAA|fbs|sec");
        var away = SeedTeam("NCAAF|NCAA|fbs|sec");
        var contest = SeedContest(home, away, Kickoff, 28, 24, winner: home);
        SeedCompetition(contest.Id, timeValid: true);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ProjectContestFactsCommandHandler>();
        await sut.ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        // A score correction, then a re-projection.
        var tracked = await FootballDataContext.Contests.FirstAsync(c => c.Id == contest.Id);
        tracked.HomeScore = 31;
        await FootballDataContext.SaveChangesAsync();
        await sut.ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        var facts = await FootballDataContext.FranchiseContestFacts.AsNoTracking()
            .Where(f => f.ContestId == contest.Id).ToListAsync();
        facts.Should().HaveCount(2);
        facts.Single(f => f.FranchiseSeasonId == home.Id).PointsFor.Should().Be(31);
    }

    [Fact]
    public async Task Execute_WhenNoTeamIsFbs_ProjectsNothing()
    {
        var contest = SeedContest(SeedTeam("NCAAF|NCAA|fcs|caa"), SeedTeam("NCAAF|NCAA|fcs|caa"), Kickoff, 14, 7, winner: null);
        SeedCompetition(contest.Id, timeValid: true);
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<ProjectContestFactsCommandHandler>()
            .ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        result.Value.Should().Be(0);
        (await FootballDataContext.FranchiseContestFacts.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Execute_WhenContestNoLongerFinalized_RemovesItsRows()
    {
        var home = SeedTeam("NCAAF|NCAA|fbs|sec");
        var away = SeedTeam("NCAAF|NCAA|fbs|sec");
        var contest = SeedContest(home, away, Kickoff, 28, 24, winner: home);
        SeedCompetition(contest.Id, timeValid: true);
        await FootballDataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ProjectContestFactsCommandHandler>();
        await sut.ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        // Re-enrichment clears FinalizedUtc before re-finalizing.
        var tracked = await FootballDataContext.Contests.FirstAsync(c => c.Id == contest.Id);
        tracked.FinalizedUtc = null;
        await FootballDataContext.SaveChangesAsync();

        var result = await sut.ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        result.Value.Should().Be(0);
        (await FootballDataContext.FranchiseContestFacts.CountAsync(f => f.ContestId == contest.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Execute_WhenCompetitionNotIngested_RemovesRows_RatherThanGuessingSiteAndConference()
    {
        var home = SeedTeam("NCAAF|NCAA|fbs|sec");
        var away = SeedTeam("NCAAF|NCAA|fbs|sec");
        var contest = SeedContest(home, away, Kickoff, 28, 24, winner: home);
        // A stale row from an earlier projection must not survive either.
        FootballDataContext.FranchiseContestFacts.Add(new FranchiseContestFact
        {
            ContestId = contest.Id,
            FranchiseSeasonId = home.Id,
            KickoffUtc = Kickoff,
        });
        await FootballDataContext.SaveChangesAsync();

        var result = await Mocker.CreateInstance<ProjectContestFactsCommandHandler>()
            .ExecuteAsync(new ProjectContestFactsCommand(contest.Id));

        result.Value.Should().Be(0);
        (await FootballDataContext.FranchiseContestFacts.CountAsync(f => f.ContestId == contest.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Execute_WhenContestMissing_ReturnsNotFound()
    {
        var result = await Mocker.CreateInstance<ProjectContestFactsCommandHandler>()
            .ExecuteAsync(new ProjectContestFactsCommand(Guid.NewGuid()));

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    private FranchiseSeason SeedTeam(string groupSeasonMap)
    {
        var fs = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, Guid.NewGuid())
            .With(x => x.SeasonYear, SeasonYear)
            .With(x => x.GroupSeasonMap, groupSeasonMap)
            .With(x => x.Name, "Team")
            .With(x => x.DisplayName, "Team")
            .With(x => x.DisplayNameShort, "Team")
            .With(x => x.Abbreviation, "TM")
            .With(x => x.Location, "City")
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.ColorCodeHex, "000000")
            .Create();
        FootballDataContext.FranchiseSeasons.Add(fs);
        return fs;
    }

    private Venue SeedVenue(string timeZone)
    {
        var venue = Fixture.Build<Venue>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Stadium")
            .With(x => x.Slug, Guid.NewGuid().ToString())
            .With(x => x.City, "City")
            .With(x => x.PostalCode, "00000")
            .With(x => x.Latitude, 30.4m)
            .With(x => x.Longitude, -91.2m)
            .With(x => x.TimeZone, timeZone)
            .Create();
        FootballDataContext.Venues.Add(venue);
        return venue;
    }

    private FootballContest SeedContest(
        FranchiseSeason home, FranchiseSeason away, DateTime kickoff,
        int homeScore, int awayScore, FranchiseSeason winner = null,
        FranchiseSeason spreadWinner = null, Venue venue = null)
    {
        var contest = Fixture.Build<FootballContest>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.HomeTeamFranchiseSeasonId, home.Id)
            .With(x => x.AwayTeamFranchiseSeasonId, away.Id)
            .With(x => x.SeasonYear, SeasonYear)
            .With(x => x.StartDateUtc, kickoff)
            .With(x => x.HomeScore, homeScore)
            .With(x => x.AwayScore, awayScore)
            .With(x => x.WinnerFranchiseSeasonId, winner?.Id)
            .With(x => x.SpreadWinnerFranchiseSeasonId, spreadWinner?.Id)
            .With(x => x.VenueId, venue?.Id)
            .With(x => x.FinalizedUtc, kickoff.AddHours(4))
            .With(x => x.Name, "Game")
            .With(x => x.ShortName, "G")
            .With(x => x.Sport, Sport.FootballNcaa)
            .Create();
        FootballDataContext.Contests.Add(contest);
        return contest;
    }

    private Guid SeedCompetition(Guid contestId, bool timeValid)
    {
        var competition = Fixture.Build<FootballCompetition>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, contestId)
            .With(x => x.TimeValid, timeValid)
            .With(x => x.Notes, new List<CompetitionNote>())
            .Create();
        FootballDataContext.Competitions.Add(competition);
        return competition.Id;
    }

    private void SeedCompetitor(Guid competitionId, FranchiseSeason team, string homeAway, int rank)
    {
        FootballDataContext.CompetitionCompetitors.Add(new FootballCompetitionCompetitor
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            FranchiseSeasonId = team.Id,
            HomeAway = homeAway,
            CuratedRankCurrent = rank,
        });
    }

    private void SeedOdds(Guid competitionId, string providerId, decimal homeSpread, decimal overUnder)
    {
        FootballDataContext.CompetitionOdds.Add(new CompetitionOdds
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            ProviderRef = new Uri($"https://example.com/odds/{providerId}"),
            ProviderId = providerId,
            ProviderName = "Book",
            Spread = homeSpread,
            OverUnder = overUnder,
            FinalizedUtc = Kickoff.AddHours(4),
        });
    }

    private void SeedBox(Guid competitionId, FranchiseSeason team, int totalYards, int sacks)
    {
        var doc = new CompetitionCompetitorStatistic
        {
            Id = Guid.NewGuid(),
            CompetitionId = competitionId,
            FranchiseSeasonId = team.Id,
        };
        FootballDataContext.CompetitionCompetitorStatistics.Add(doc);
        AddStat(doc.Id, "passing", "netTotalYards", totalYards);
        AddStat(doc.Id, "defensive", "sacks", sacks);
    }

    private void AddStat(Guid docId, string category, string name, decimal value)
    {
        var cat = new CompetitionCompetitorStatisticCategory
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorStatisticId = docId,
            Name = category,
        };
        cat.Stats.Add(new CompetitionCompetitorStatisticStat
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorStatisticCategoryId = cat.Id,
            Name = name,
            DisplayName = name,
            ShortDisplayName = name,
            Abbreviation = name,
            Value = value,
            DisplayValue = value.ToString(),
        });
        FootballDataContext.CompetitionCompetitorStatisticCategories.Add(cat);
    }
}
