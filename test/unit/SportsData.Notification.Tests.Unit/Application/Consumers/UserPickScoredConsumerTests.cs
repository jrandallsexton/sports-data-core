using FluentAssertions;

using MassTransit;

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
        string leagueName = "Sluggers")
        => new(
            userId, null, Guid.NewGuid(), null, null,
            awayAbbr, homeAbbr, awayScore, homeScore,
            isCorrect, pickedIsHome, pickedSpread,
            Guid.NewGuid(), leagueName, Sport.BaseballMlb, 2026,
            Guid.NewGuid(), Guid.NewGuid());

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
}
