using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

// Covers the message composition (ComposeBody) via the public Consume path —
// the point of moving formatting into this service. The push sender is mocked
// to capture the rendered title/body.
public class UserPickScoredConsumerTests : NotificationTestBase<UserPickScoredConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 7, 13, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IPushNotificationSender> _pushSender;
    private string _capturedTitle;
    private string _capturedBody;

    public UserPickScoredConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);

        _pushSender = Mocker.GetMock<IPushNotificationSender>();
        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, string, IReadOnlyDictionary<string, string>, CancellationToken>(
                (_, title, body, _, _) => { _capturedTitle = title; _capturedBody = body; })
            .ReturnsAsync(new Success<string>("msg-id"));
    }

    private static ConsumeContext<UserPickScored> ContextFor(UserPickScored msg)
    {
        var ctx = new Mock<ConsumeContext<UserPickScored>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private async Task SeedDeviceAsync(Guid userId)
    {
        DataContext.UserDevices.Add(new UserDevice
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            InstallationId = Guid.NewGuid().ToString(),
            FcmToken = "tok",
            Platform = "ios",
            NotificationsEnabled = true,
            LastSeenUtc = FixedNow,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();
    }

    private static UserPickScored Msg(
        Guid userId,
        string awayAbbr,
        string homeAbbr,
        int awayScore,
        int homeScore,
        bool? isCorrect,
        bool? pickedIsHome,
        double? pickedSpread,
        string leagueName = "Sluggers",
        Guid? pickId = null,
        Guid? contestId = null,
        Guid? leagueId = null,
        Guid? correlationId = null)
        => new(
            userId, null, contestId ?? Guid.NewGuid(), pickId ?? Guid.NewGuid(), null, null,
            awayAbbr, homeAbbr, awayScore, homeScore,
            isCorrect, pickedIsHome, pickedSpread,
            leagueId ?? Guid.NewGuid(), leagueName, Sport.BaseballMlb, 2026,
            correlationId ?? Guid.NewGuid(), Guid.NewGuid());

    private async Task<string> RunAndCaptureBodyAsync(UserPickScored msg)
    {
        await SeedDeviceAsync(msg.UserId);
        var sut = Mocker.CreateInstance<UserPickScoredConsumer>();
        await sut.Consume(ContextFor(msg));
        return _capturedBody;
    }

    [Fact]
    public async Task Consume_StraightUpWin_PickedAway_ComposesScorelineFirst()
    {
        // BOS (away) picked, won 3-2. No spread.
        var body = await RunAndCaptureBodyAsync(
            Msg(Guid.NewGuid(), "BOS", "NYY", awayScore: 3, homeScore: 2,
                isCorrect: true, pickedIsHome: false, pickedSpread: null));

        body.Should().Be("Sluggers: BOS 3, NYY 2 — you picked BOS ✓");
        _capturedTitle.Should().Be("Nice pick!");
    }

    [Fact]
    public async Task Consume_StraightUpLoss_PickedHome_MarksIncorrect()
    {
        // NYY (home) picked, lost 3-2 (away won). No spread.
        var body = await RunAndCaptureBodyAsync(
            Msg(Guid.NewGuid(), "BOS", "NYY", awayScore: 3, homeScore: 2,
                isCorrect: false, pickedIsHome: true, pickedSpread: null));

        body.Should().Be("Sluggers: NYY 2, BOS 3 — you picked NYY ✗");
        _capturedTitle.Should().Be("Tough loss");
    }

    [Fact]
    public async Task Consume_AtsCoveredLoss_PickedAwayDog_ShowsSpreadAndCheck()
    {
        // BOS (away) +2.5 picked, lost 2-3 (covered by losing < 2.5) → ✓.
        var body = await RunAndCaptureBodyAsync(
            Msg(Guid.NewGuid(), "BOS", "NYY", awayScore: 2, homeScore: 3,
                isCorrect: true, pickedIsHome: false, pickedSpread: 2.5));

        body.Should().Be("Sluggers: BOS 2, NYY 3 — you picked BOS +2.5 ✓");
    }

    [Fact]
    public async Task Consume_AtsUncoveredWin_PickedHomeFavorite_ShowsSpreadAndCross()
    {
        // NYY (home) -3 picked, won 3-2 (won by 1, didn't cover) → ✗.
        var body = await RunAndCaptureBodyAsync(
            Msg(Guid.NewGuid(), "BOS", "NYY", awayScore: 2, homeScore: 3,
                isCorrect: false, pickedIsHome: true, pickedSpread: -3));

        body.Should().Be("Sluggers: NYY 3, BOS 2 — you picked NYY -3 ✗");
    }

    [Fact]
    public async Task Consume_MissingAbbreviations_FallsBackToGenericCopy()
    {
        // Unfattened event (no abbreviations) → generic shape, no crash.
        var body = await RunAndCaptureBodyAsync(
            Msg(Guid.NewGuid(), awayAbbr: null, homeAbbr: null, awayScore: 3, homeScore: 2,
                isCorrect: true, pickedIsHome: null, pickedSpread: null));

        body.Should().Contain("Your pick won.");
        body.Should().NotContain("you picked");
    }

    [Fact]
    public async Task Consume_PersistsTypedRow_WithPickMetadata()
    {
        // The whole point of the typed table: the audit row carries the
        // notification's subject (PickId/ContestId/LeagueId) as real columns.
        var userId = Guid.NewGuid();
        var pickId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var leagueId = Guid.NewGuid();

        await RunAndCaptureBodyAsync(
            Msg(userId, "BOS", "NYY", awayScore: 3, homeScore: 2,
                isCorrect: true, pickedIsHome: false, pickedSpread: null,
                pickId: pickId, contestId: contestId, leagueId: leagueId));

        var row = await DataContext.NotificationUserPicks.SingleAsync();
        row.UserId.Should().Be(userId);
        row.PickId.Should().Be(pickId);
        row.ContestId.Should().Be(contestId);
        row.LeagueId.Should().Be(leagueId);
        row.Channel.Should().Be("Fcm");
        row.Result.Should().Be("Sent");
        row.Title.Should().Be("Nice pick!");
        row.Body.Should().Be("Sluggers: BOS 3, NYY 2 — you picked BOS ✓");
    }

    [Fact]
    public async Task Consume_SameUserAndContest_DistinctPicks_EachNotifies()
    {
        // A user in three leagues who picked the same game has three distinct
        // PickIds — but all three events come from ONE scoring run and therefore
        // share ONE CorrelationId. The old (CorrelationId, UserId, Channel) key
        // collapsed them to a single push; (UserId, PickId) preserves all three.
        // The shared CorrelationId is the whole point: it's exactly what the old
        // key keyed on. One device, three picks → three rows and three sends.
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        foreach (var _ in Enumerable.Range(0, 3))
        {
            var msg = Msg(userId, "BOS", "NYY", awayScore: 3, homeScore: 2,
                isCorrect: true, pickedIsHome: false, pickedSpread: null,
                pickId: Guid.NewGuid(), contestId: contestId, leagueId: Guid.NewGuid(),
                correlationId: correlationId);
            var sut = Mocker.CreateInstance<UserPickScoredConsumer>();
            await sut.Consume(ContextFor(msg));
        }

        (await DataContext.NotificationUserPicks.CountAsync()).Should().Be(3);
        _pushSender.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    // Note: the duplicate-suppression path (a re-scored pick / redelivery on a
    // different CorrelationId colliding on the (UserId, PickId) unique index and
    // hitting the "already claimed" branch) relies on Postgres raising 23505.
    // The InMemory provider does not enforce unique indexes, so that branch is
    // validated against the local migration DB rather than here.

    [Fact]
    public async Task Consume_DeadFcmToken_PrunesDevice()
    {
        // FCM reports the token dead (sender maps Unregistered/InvalidArgument to
        // NotFound). The device row should be pruned so it stops failing forever;
        // it re-registers on next app launch.
        var userId = Guid.NewGuid();
        await SeedDeviceAsync(userId);

        _pushSender
            .Setup(x => x.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<IReadOnlyDictionary<string, string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<string>(string.Empty, ResultStatus.NotFound, []));

        var msg = Msg(userId, "BOS", "NYY", awayScore: 3, homeScore: 2,
            isCorrect: true, pickedIsHome: false, pickedSpread: null);

        var sut = Mocker.CreateInstance<UserPickScoredConsumer>();
        await sut.Consume(ContextFor(msg));

        (await DataContext.UserDevices.CountAsync(d => d.UserId == userId)).Should().Be(0);
    }
}
