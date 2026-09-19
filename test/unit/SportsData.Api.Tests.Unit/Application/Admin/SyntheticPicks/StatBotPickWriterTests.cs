using System.Linq.Expressions;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Scoring;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Processing;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Admin.SyntheticPicks;

/// <summary>
/// StatBot's picks are ordinary UserPick rows, one per (league, contest),
/// derived from the latest non-rejected preview. These pin the rules that
/// make that record honest: every league gets the row, ATS leagues take the
/// spread winner, a changed preview changes an unlocked pick, nothing is
/// written after kickoff, and a backfill only fills games whose preview
/// predates the kickoff.
/// </summary>
public class StatBotPickWriterTests : ApiTestBase<StatBotPickWriter>
{
    private static readonly DateTime Now = new(2026, 9, 16, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid StatBot = IStatBotPickWriter.StatBotUserId;
    private static readonly Guid Home = Guid.NewGuid();
    private static readonly Guid Away = Guid.NewGuid();

    public StatBotPickWriterTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
    }

    [Fact]
    public async Task UpsertForContest_WritesOnePickPerLeague_SpreadWinnerForAtsAndStraightUpOtherwise()
    {
        var contestId = Guid.NewGuid();
        var su = await SeedLeagueAsync(PickType.StraightUp);
        var ats = await SeedLeagueAsync(PickType.AgainstTheSpread);
        await SeedMatchupAsync(su.Id, contestId, kickoff: Now.AddDays(3), homeSpread: -6.5);
        await SeedMatchupAsync(ats.Id, contestId, kickoff: Now.AddDays(3), homeSpread: -6.5);
        await SeedPreviewAsync(contestId, straightUp: Home, spread: Away, createdUtc: Now.AddHours(-1));

        var sut = Mocker.CreateInstance<StatBotPickWriter>();
        var written = await sut.UpsertForContestAsync(contestId);

        written.Should().Be(2);
        var picks = await DataContext.UserPicks.Where(p => p.UserId == StatBot).ToListAsync();
        picks.Should().HaveCount(2);
        picks.Single(p => p.PickemGroupId == su.Id).FranchiseSeasonId.Should().Be(Home);
        picks.Single(p => p.PickemGroupId == ats.Id).FranchiseSeasonId.Should().Be(Away);
        picks.Should().OnlyContain(p => p.ContestId == contestId && p.Week == 3);
        // Dated when the AI decided, not when the row was written.
        picks.Should().OnlyContain(p => p.CreatedUtc == Now.AddHours(-1));
    }

    [Fact]
    public async Task UpsertForContest_AtsLeagueWithoutALine_FallsBackToStraightUpWinner()
    {
        var contestId = Guid.NewGuid();
        var ats = await SeedLeagueAsync(PickType.AgainstTheSpread);
        await SeedMatchupAsync(ats.Id, contestId, kickoff: Now.AddDays(3), homeSpread: null);
        await SeedPreviewAsync(contestId, straightUp: Home, spread: Away, createdUtc: Now.AddHours(-1));

        await Mocker.CreateInstance<StatBotPickWriter>().UpsertForContestAsync(contestId);

        (await DataContext.UserPicks.SingleAsync()).FranchiseSeasonId.Should().Be(Home);
    }

    [Fact]
    public async Task UpsertForContest_ANewerPreviewNamingADifferentWinner_UpdatesTheUnlockedPick()
    {
        var contestId = Guid.NewGuid();
        var su = await SeedLeagueAsync(PickType.StraightUp);
        await SeedMatchupAsync(su.Id, contestId, kickoff: Now.AddDays(3), homeSpread: null);
        await SeedPreviewAsync(contestId, straightUp: Home, spread: null, createdUtc: Now.AddHours(-2));
        var sut = Mocker.CreateInstance<StatBotPickWriter>();
        await sut.UpsertForContestAsync(contestId);

        // Rejected, regenerated, and the new one flips the call.
        await SeedPreviewAsync(contestId, straightUp: Away, spread: null, createdUtc: Now.AddHours(-1));
        var written = await sut.UpsertForContestAsync(contestId);

        written.Should().Be(1);
        var pick = await DataContext.UserPicks.SingleAsync(p => p.UserId == StatBot);
        pick.FranchiseSeasonId.Should().Be(Away);
        pick.ModifiedUtc.Should().Be(Now);
    }

    [Fact]
    public async Task UpsertForContest_SamePreviewAgain_WritesNothing()
    {
        var contestId = Guid.NewGuid();
        var su = await SeedLeagueAsync(PickType.StraightUp);
        await SeedMatchupAsync(su.Id, contestId, kickoff: Now.AddDays(3), homeSpread: null);
        await SeedPreviewAsync(contestId, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1));
        var sut = Mocker.CreateInstance<StatBotPickWriter>();
        await sut.UpsertForContestAsync(contestId);

        // At-least-once redelivery of the same event.
        var written = await sut.UpsertForContestAsync(contestId);

        written.Should().Be(0);
        (await DataContext.UserPicks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task UpsertForContest_AfterKickoff_NeitherInsertsNorChanges()
    {
        var contestId = Guid.NewGuid();
        var su = await SeedLeagueAsync(PickType.StraightUp);
        await SeedMatchupAsync(su.Id, contestId, kickoff: Now.AddMinutes(-30), homeSpread: null);
        // An existing pick made before kickoff...
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(), UserId = StatBot, PickemGroupId = su.Id, ContestId = contestId, Week = 3,
            FranchiseSeasonId = Home, PickType = PickType.StraightUp, CreatedUtc = Now.AddDays(-1), CreatedBy = StatBot
        });
        await DataContext.SaveChangesAsync();
        // ...and a preview generated after the game started that disagrees.
        await SeedPreviewAsync(contestId, straightUp: Away, spread: null, createdUtc: Now.AddMinutes(-5));

        var written = await Mocker.CreateInstance<StatBotPickWriter>().UpsertForContestAsync(contestId);

        written.Should().Be(0);
        (await DataContext.UserPicks.SingleAsync()).FranchiseSeasonId.Should().Be(Home);
    }

    [Fact]
    public async Task UpsertForContest_NoPreview_OrEmptyWinner_WritesNothing()
    {
        var contestId = Guid.NewGuid();
        var su = await SeedLeagueAsync(PickType.StraightUp);
        await SeedMatchupAsync(su.Id, contestId, kickoff: Now.AddDays(3), homeSpread: null);
        var sut = Mocker.CreateInstance<StatBotPickWriter>();

        (await sut.UpsertForContestAsync(contestId)).Should().Be(0);

        // Guid.Empty is "no prediction", never a pick.
        await SeedPreviewAsync(contestId, straightUp: Guid.Empty, spread: null, createdUtc: Now.AddHours(-1));
        (await sut.UpsertForContestAsync(contestId)).Should().Be(0);
        (await DataContext.UserPicks.AnyAsync()).Should().BeFalse();
    }

    [Fact]
    public async Task BackfillWeek_FillsMissingPicksAcrossLeagues_OnlyWherePreviewPredatesKickoff()
    {
        var su = await SeedLeagueAsync(PickType.StraightUp);
        var ats = await SeedLeagueAsync(PickType.AgainstTheSpread);

        // Played last week; preview existed two days before kickoff -> fill.
        var played = Guid.NewGuid();
        await SeedMatchupAsync(su.Id, played, kickoff: Now.AddDays(-7), homeSpread: -3, week: 2);
        await SeedMatchupAsync(ats.Id, played, kickoff: Now.AddDays(-7), homeSpread: -3, week: 2);
        await SeedPreviewAsync(played, straightUp: Home, spread: Away, createdUtc: Now.AddDays(-9));

        // Played last week; preview written after kickoff -> never makeable, skip.
        var late = Guid.NewGuid();
        await SeedMatchupAsync(su.Id, late, kickoff: Now.AddDays(-7), homeSpread: null, week: 2);
        await SeedPreviewAsync(late, straightUp: Home, spread: null, createdUtc: Now.AddDays(-6));

        // Already picked (with a different winner) -> backfill never updates.
        var already = Guid.NewGuid();
        await SeedMatchupAsync(su.Id, already, kickoff: Now.AddDays(-7), homeSpread: null, week: 2);
        await SeedPreviewAsync(already, straightUp: Home, spread: null, createdUtc: Now.AddDays(-9));
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(), UserId = StatBot, PickemGroupId = su.Id, ContestId = already, Week = 2,
            FranchiseSeasonId = Away, PickType = PickType.StraightUp, CreatedUtc = Now.AddDays(-9), CreatedBy = StatBot
        });

        // Different week -> out of scope.
        var otherWeek = Guid.NewGuid();
        await SeedMatchupAsync(su.Id, otherWeek, kickoff: Now.AddDays(3), homeSpread: null, week: 3);
        await SeedPreviewAsync(otherWeek, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1));
        await DataContext.SaveChangesAsync();

        var inserted = await Mocker.CreateInstance<StatBotPickWriter>().BackfillWeekAsync(2026, 2);

        inserted.Should().Be(2);
        var picks = await DataContext.UserPicks.Where(p => p.UserId == StatBot).ToListAsync();
        picks.Where(p => p.ContestId == played).Should().HaveCount(2);
        picks.Single(p => p.ContestId == played && p.PickemGroupId == ats.Id).FranchiseSeasonId.Should().Be(Away);
        picks.Should().NotContain(p => p.ContestId == late);
        picks.Single(p => p.ContestId == already).FranchiseSeasonId.Should().Be(Away);
        picks.Should().NotContain(p => p.ContestId == otherWeek);
    }

    /// <summary>
    /// Confidence leagues score a pick by its assigned confidence
    /// (PickScoringService: ConfidencePoints ?? 0), so a null means StatBot
    /// scores ZERO on every correct pick — last place by construction. Points
    /// go to the game the model was most sure of, measured by the preview's own
    /// predicted margin. Slate order was the alternative and is strictly worse:
    /// it stakes the most points on whichever game kicks off first.
    /// </summary>
    [Fact]
    public async Task UpsertForContest_ConfidenceLeague_RanksByPredictedMargin()
    {
        var league = await SeedLeagueAsync(PickType.StraightUp, useConfidencePoints: true);

        // Three contests, deliberately seeded so slate order DISAGREES with
        // margin order — the earliest kickoff is the closest game.
        var close = Guid.NewGuid();
        var blowout = Guid.NewGuid();
        var middling = Guid.NewGuid();
        await SeedMatchupAsync(league.Id, close, kickoff: Now.AddDays(1), homeSpread: null);
        await SeedMatchupAsync(league.Id, middling, kickoff: Now.AddDays(2), homeSpread: null);
        await SeedMatchupAsync(league.Id, blowout, kickoff: Now.AddDays(3), homeSpread: null);
        await SeedPreviewAsync(close, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1), awayScore: 20, homeScore: 21);
        await SeedPreviewAsync(middling, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1), awayScore: 10, homeScore: 24);
        await SeedPreviewAsync(blowout, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1), awayScore: 3, homeScore: 45);

        var sut = Mocker.CreateInstance<StatBotPickWriter>();
        await sut.UpsertForContestAsync(close);
        await sut.UpsertForContestAsync(middling);
        await sut.UpsertForContestAsync(blowout);

        var picks = await DataContext.UserPicks
            .Where(p => p.UserId == StatBot && p.PickemGroupId == league.Id)
            .ToDictionaryAsync(p => p.ContestId, p => p.ConfidencePoints);

        picks[blowout].Should().Be(3, "42-point margin is the model's surest call");
        picks[middling].Should().Be(2);
        picks[close].Should().Be(1, "a 1-point margin is the least confident");
    }

    [Fact]
    public async Task UpsertForContest_NonConfidenceLeague_LeavesConfidenceNull()
    {
        var league = await SeedLeagueAsync(PickType.StraightUp);
        var contestId = Guid.NewGuid();
        await SeedMatchupAsync(league.Id, contestId, kickoff: Now.AddDays(3), homeSpread: null);
        await SeedPreviewAsync(contestId, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1), awayScore: 3, homeScore: 45);

        await Mocker.CreateInstance<StatBotPickWriter>().UpsertForContestAsync(contestId);

        (await DataContext.UserPicks.SingleAsync()).ConfidencePoints.Should().BeNull();
    }

    /// <summary>
    /// Every pick in a confidence league must carry a value, so a preview with
    /// no predicted score sorts last rather than dropping out of the ranking.
    /// </summary>
    [Fact]
    public async Task UpsertForContest_ConfidenceLeague_PreviewWithoutAPredictedScoreStillRanks()
    {
        var league = await SeedLeagueAsync(PickType.StraightUp, useConfidencePoints: true);
        var scored = Guid.NewGuid();
        var unscored = Guid.NewGuid();
        await SeedMatchupAsync(league.Id, scored, kickoff: Now.AddDays(1), homeSpread: null);
        await SeedMatchupAsync(league.Id, unscored, kickoff: Now.AddDays(2), homeSpread: null);
        await SeedPreviewAsync(scored, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1), awayScore: 7, homeScore: 28);
        await SeedPreviewAsync(unscored, straightUp: Home, spread: null, createdUtc: Now.AddHours(-1));

        var sut = Mocker.CreateInstance<StatBotPickWriter>();
        await sut.UpsertForContestAsync(scored);
        await sut.UpsertForContestAsync(unscored);

        var picks = await DataContext.UserPicks
            .Where(p => p.UserId == StatBot)
            .ToDictionaryAsync(p => p.ContestId, p => p.ConfidencePoints);

        picks[scored].Should().Be(2);
        picks[unscored].Should().Be(1);
        picks.Values.Should().OnlyContain(v => v != null);
    }

    /// <summary>
    /// A backfilled week is one whose contests already finalized, so
    /// ContestFinalized has fired and will not fire again. Without an explicit
    /// enqueue these picks sit unscored forever.
    /// </summary>
    [Fact]
    public async Task BackfillWeek_EnqueuesScoringForEveryContestItTouched()
    {
        var league = await SeedLeagueAsync(PickType.StraightUp);
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        await SeedMatchupAsync(league.Id, a, kickoff: Now.AddDays(-7), homeSpread: null, week: 2);
        await SeedMatchupAsync(league.Id, b, kickoff: Now.AddDays(-7), homeSpread: null, week: 2);
        await SeedPreviewAsync(a, straightUp: Home, spread: null, createdUtc: Now.AddDays(-9));
        await SeedPreviewAsync(b, straightUp: Away, spread: null, createdUtc: Now.AddDays(-9));

        var scored = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IScorePicks>(It.IsAny<Expression<Func<IScorePicks, Task>>>()))
            .Callback<Expression<Func<IScorePicks, Task>>>(expr =>
            {
                var call = (MethodCallExpression)expr.Body;
                var cmd = (ScorePicksCommand)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!;
                scored.Add(cmd.ContestId);
            });

        var inserted = await Mocker.CreateInstance<StatBotPickWriter>().BackfillWeekAsync(2026, 2);

        inserted.Should().Be(2);
        scored.Should().BeEquivalentTo([a, b]);
    }

    private async Task<PickemGroup> SeedLeagueAsync(PickType pickType, bool useConfidencePoints = false)
    {
        var commissioner = Guid.NewGuid();
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = $"League {pickType}",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = pickType,
            UseConfidencePoints = useConfidencePoints,
            CommissionerUserId = commissioner,
            SeasonYear = 2026,
            CreatedUtc = Now.AddDays(-30),
            CreatedBy = commissioner
        };
        DataContext.PickemGroups.Add(group);
        await DataContext.SaveChangesAsync();
        return group;
    }

    private async Task SeedMatchupAsync(Guid groupId, Guid contestId, DateTime kickoff, double? homeSpread, int week = 3)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2026,
            SeasonWeek = week,
            StartDateUtc = kickoff,
            HomeSpread = homeSpread,
            CreatedUtc = Now.AddDays(-10),
            CreatedBy = Guid.Empty
        });
        await DataContext.SaveChangesAsync();
    }

    private async Task SeedPreviewAsync(Guid contestId, Guid? straightUp, Guid? spread, DateTime createdUtc, int? awayScore = null, int? homeScore = null)
    {
        DataContext.MatchupPreviews.Add(new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = contestId,
            PromptId = Guid.NewGuid(),
            PredictedStraightUpWinner = straightUp,
            PredictedSpreadWinner = spread,
            AwayScore = awayScore,
            HomeScore = homeScore,
            CreatedUtc = createdUtc,
            CreatedBy = Guid.Empty
        });
        await DataContext.SaveChangesAsync();
    }
}
