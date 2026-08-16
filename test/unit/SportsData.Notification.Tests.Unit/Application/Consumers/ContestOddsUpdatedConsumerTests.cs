using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Contest.Queries;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class ContestOddsUpdatedConsumerTests : NotificationTestBase<ContestOddsUpdatedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;

    public ContestOddsUpdatedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<string>("msg-id"));
    }

    private static ConsumeContext<ContestOddsUpdated> ContextFor(ContestOddsUpdated msg)
    {
        var ctx = new Mock<ConsumeContext<ContestOddsUpdated>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static ContestOddsUpdated Msg(
        Guid contestId,
        decimal? oldSpread = null, decimal? newSpread = null,
        decimal? oldTotal = null, decimal? newTotal = null)
        => new(contestId, "odds updated", "1", "DraftKings",
            oldSpread, newSpread, oldTotal, newTotal,
            null, Sport.FootballNcaa, 2026, Guid.NewGuid(), Guid.NewGuid());

    private async Task SeedPickAsync(Guid userId, Guid contestId, Guid groupId, string pickType)
    {
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = "League",
            Sport = Sport.FootballNcaa,
            CommissionerUserId = Guid.NewGuid(),
            PickType = pickType,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        DataContext.UserPicks.Add(new UserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestId = contestId,
            PickemGroupId = groupId,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private async Task SeedDeviceAsync(Guid userId, bool enabled = true)
    {
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = Guid.NewGuid().ToString(),
            FcmToken = "tok",
            Platform = "ios",
            NotificationsEnabled = enabled,
            LastSeenUtc = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private void VerifySendCount(Times times) =>
        _pushSender.Verify(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()), times);

    [Fact]
    public async Task Consume_NoMovement_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -3m, oldTotal: 50m, newTotal: 50m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_StraightUpLeague_DoesNotNotify_EvenWhenSpreadMoves()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.StraightUp);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_AtsLeague_SpreadMoved_Notifies()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_AtsLeague_OnlyTotalMoved_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldTotal: 50m, newTotal: 54m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_OverUnderLeague_TotalMoved_Notifies()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.OverUnder);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldTotal: 50m, newTotal: 54m)));

        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_SamePickerInTwoQualifyingLeagues_NotifiesOnce()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        // One device, one user, deduped across leagues -> a single push.
        VerifySendCount(Times.Once());
    }

    [Fact]
    public async Task Consume_OptedOut_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);
        DataContext.UserNotificationPreferences.Add(new UserNotificationPreferences
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            OddsChangedEnabled = false,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_NoEnabledDevice_DoesNotNotify()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId, enabled: false);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    [Fact]
    public async Task Consume_MissingLeagueProjection_DoesNotNotify()
    {
        // Pick exists but the PickemGroup projection hasn't landed yet — the
        // inner join drops it rather than mis-targeting.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        DataContext.UserPicks.Add(new UserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestId = contestId,
            PickemGroupId = Guid.NewGuid(),
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
        await SeedDeviceAsync(userId);

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -6m)));

        VerifySendCount(Times.Never());
    }

    // ─── Enrichment + deep link ───────────────────────────────────────────

    private const string ShortName = "LSU @ BAMA";
    private const string FullName = "LSU Tigers at Alabama Crimson Tide";

    /// <summary>
    /// Stubs Producer's contest lookup. The consumer resolves the client per
    /// sport off IContestClientFactory.
    /// </summary>
    private void StubContest(SeasonContestDto contest)
    {
        var client = new Mock<IProvideContests>();
        client
            .Setup(x => x.GetContestById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<GetContestByIdResponse>(new GetContestByIdResponse(contest)));

        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(client.Object);
    }

    private void StubContestThrows()
    {
        var client = new Mock<IProvideContests>();
        client
            .Setup(x => x.GetContestById(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("producer unreachable"));

        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(client.Object);
    }

    private (string Title, string Body, IReadOnlyDictionary<string, string> Data) CapturedSend()
    {
        string title = null, body = null;
        IReadOnlyDictionary<string, string> data = null;
        _pushSender.Verify(x => x.SendAsync(
            It.IsAny<string>(),
            It.Is<string>(t => Capture(t, ref title)),
            It.Is<string>(b => Capture(b, ref body)),
            It.Is<IReadOnlyDictionary<string, string>>(d => CaptureData(d, ref data)),
            It.IsAny<CancellationToken>()), Times.Once);
        return (title, body, data);
    }

    private static bool Capture(string value, ref string slot) { slot = value; return true; }
    private static bool CaptureData(IReadOnlyDictionary<string, string> value, ref IReadOnlyDictionary<string, string> slot)
    { slot = value; return true; }

    [Fact]
    public async Task Consume_EnrichedContest_PutsMatchupInTitleAndDeepLinkInData()
    {
        // The reported defect: stacked "Line moved" alerts were
        // indistinguishable because no copy named the game.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, groupId, LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        StubContest(new SeasonContestDto
        {
            Id = contestId,
            Name = FullName,
            ShortName = ShortName,
            Week = 3
        });

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -1.5m)));

        var (title, body, data) = CapturedSend();

        title.Should().Be($"Line moved: {ShortName}");
        body.Should().StartWith(FullName, "the full name disambiguates the abbreviated title");
        body.Should().Contain("-3").And.Contain("-1.5");

        data["kind"].Should().Be("OddsChanged");
        data["contestId"].Should().Be(contestId.ToString());
        data["leagueId"].Should().Be(groupId.ToString());
        data["sport"].Should().Be(nameof(Sport.FootballNcaa));
        data["week"].Should().Be("3");
    }

    [Fact]
    public async Task Consume_ContestLookupFails_StillSendsUnEnriched()
    {
        // Enrichment is additive: losing Producer costs the matchup name and
        // the deep link, never the notification.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        await SeedPickAsync(userId, contestId, Guid.NewGuid(), LeaguePickType.AgainstTheSpread);
        await SeedDeviceAsync(userId);

        StubContestThrows();

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -1.5m)));

        var (title, body, data) = CapturedSend();

        title.Should().Be("Line moved");
        body.Should().Contain("a game you picked");
        data["kind"].Should().Be("OddsChanged");
        data.ContainsKey("week").Should().BeFalse("no contest means no week");
    }

    [Fact]
    public async Task Consume_UserInMixedPickTypeLeagues_DeepLinksToTheQualifyingLeague()
    {
        // The trap: users pick the same contest in leagues of different types,
        // and the OLDEST is frequently StraightUp — where a line move is
        // irrelevant. The target must come from the odds-sensitive set, not
        // from "earliest pick" overall.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var straightUpId = Guid.NewGuid();
        var atsId = Guid.NewGuid();

        // StraightUp league created FIRST, so any naive ordering picks it.
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = straightUpId,
            Name = "SU league",
            Sport = Sport.FootballNcaa,
            CommissionerUserId = Guid.NewGuid(),
            PickType = LeaguePickType.StraightUp,
            CreatedUtc = FixedNow.AddDays(-30),
            CreatedBy = Guid.NewGuid()
        });
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = atsId,
            Name = "ATS league",
            Sport = Sport.FootballNcaa,
            CommissionerUserId = Guid.NewGuid(),
            PickType = LeaguePickType.AgainstTheSpread,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        foreach (var gid in new[] { straightUpId, atsId })
        {
            DataContext.UserPicks.Add(new UserPick
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ContestId = contestId,
                PickemGroupId = gid,
                CreatedUtc = FixedNow.AddDays(-1),
                CreatedBy = Guid.NewGuid()
            });
        }
        await DataContext.SaveChangesAsync();
        await SeedDeviceAsync(userId);

        StubContest(new SeasonContestDto { Id = contestId, Name = FullName, ShortName = ShortName });

        var sut = Mocker.CreateInstance<ContestOddsUpdatedConsumer>();
        await sut.Consume(ContextFor(Msg(contestId, oldSpread: -3m, newSpread: -1.5m)));

        // Notified once (deduped across leagues), targeting the ATS league.
        var (_, _, data) = CapturedSend();
        data["leagueId"].Should().Be(atsId.ToString(),
            "the deep link must land on a league where the line actually matters");
    }
}
