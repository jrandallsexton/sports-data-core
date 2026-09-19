using MassTransit;

using Microsoft.AspNetCore.SignalR;

using Moq;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Previews;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Previews;

/// <summary>
/// A generated preview must evict the league-week caches the contest sits in
/// BEFORE the SignalR broadcast that makes the client refetch - otherwise the
/// refetch lands on the stale entry and the card keeps saying "no preview" for
/// the rest of the TTL (observed 2026-09-14).
/// </summary>
public class PreviewGeneratedHandlerTests : ApiTestBase<PreviewGeneratedHandler>
{
    [Fact]
    public async Task Consume_WritesStatBotsPick_ThenEvicts_ThenBroadcasts()
    {
        var contestId = Guid.NewGuid();
        var calls = new List<string>();

        Mocker.GetMock<IStatBotPickWriter>()
            .Setup(w => w.UpsertForContestAsync(contestId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("pick"))
            .ReturnsAsync(1);

        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Setup(i => i.EvictForContestAsync(contestId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("evict"))
            .Returns(Task.CompletedTask);

        var clients = WireHubContext();
        clients.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("broadcast"))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<PreviewGeneratedHandler>();

        await sut.Consume(ContextFor(Message(contestId)));

        // The refetch the broadcast triggers must see the pick AND the preview.
        Assert.Equal(new[] { "pick", "evict", "broadcast" }, calls);
        clients.Verify(c => c.SendCoreAsync(
                nameof(PreviewGenerated),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static PreviewGenerated Message(Guid contestId) => new(
        ContestId: contestId,
        Message: "Away @ Home preview generated",
        Ref: null,
        Sport: Sport.FootballNcaa,
        SeasonYear: 2026,
        CorrelationId: Guid.NewGuid(),
        CausationId: Guid.NewGuid());

    private static ConsumeContext<PreviewGenerated> ContextFor(PreviewGenerated message) =>
        Mock.Of<ConsumeContext<PreviewGenerated>>(ctx =>
            ctx.Message == message && ctx.CancellationToken == CancellationToken.None);

    private Mock<IClientProxy> WireHubContext()
    {
        var hubContext = Mocker.GetMock<IHubContext<NotificationHub>>();
        var hubClients = new Mock<IHubClients>();
        var clients = new Mock<IClientProxy>();
        hubClients.Setup(c => c.All).Returns(clients.Object);
        hubContext.Setup(h => h.Clients).Returns(hubClients.Object);
        return clients;
    }
}
