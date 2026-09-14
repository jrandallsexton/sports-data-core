using MassTransit;

using Microsoft.AspNetCore.SignalR;

using Moq;

using SportsData.Api.Application.Previews;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Previews;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Previews;

/// <summary>
/// The league-week matchups payload carries IsPreviewAvailable per matchup and is
/// cached per (league, week). A generated preview must evict every league-week the
/// contest appears in, and must do so BEFORE the SignalR broadcast that makes the
/// client refetch - otherwise the refetch lands on the stale entry and the card
/// keeps saying "no preview" for the rest of the TTL (observed 2026-09-14).
/// </summary>
public class PreviewGeneratedHandlerTests : ApiTestBase<PreviewGeneratedHandler>
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Consume_EvictsEveryLeagueWeekContainingTheContest_ThenBroadcasts()
    {
        var contestId = Guid.NewGuid();
        var leagueA = Guid.NewGuid();
        var leagueB = Guid.NewGuid();
        var otherContest = Guid.NewGuid();

        // Same contest in two leagues (week 3), plus an unrelated matchup that must
        // not be evicted.
        DataContext.PickemGroupMatchups.AddRange(
            Matchup(leagueA, contestId, week: 3),
            Matchup(leagueB, contestId, week: 3),
            Matchup(leagueA, otherContest, week: 4));
        await DataContext.SaveChangesAsync();

        var calls = new List<string>();
        var cache = Mocker.GetMock<ILeagueWeekMatchupsCache>();
        cache.Setup(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Callback<Guid, int>((g, w) => calls.Add($"evict:{g}:{w}"))
            .Returns(Task.CompletedTask);

        var clients = WireHubContext();
        clients.Setup(c => c.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("broadcast"))
            .Returns(Task.CompletedTask);

        var sut = Mocker.CreateInstance<PreviewGeneratedHandler>();

        await sut.Consume(ContextFor(Message(contestId)));

        cache.Verify(c => c.RemoveAsync(leagueA, 3), Times.Once);
        cache.Verify(c => c.RemoveAsync(leagueB, 3), Times.Once);
        cache.Verify(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Exactly(2));

        Assert.Equal("broadcast", calls.Last());
        Assert.Equal(2, calls.Count(c => c.StartsWith("evict:")));
        clients.Verify(c => c.SendCoreAsync(
                nameof(PreviewGenerated),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_StillBroadcasts_WhenNoLeagueCarriesTheContest()
    {
        var clients = WireHubContext();
        var sut = Mocker.CreateInstance<PreviewGeneratedHandler>();

        await sut.Consume(ContextFor(Message(Guid.NewGuid())));

        Mocker.GetMock<ILeagueWeekMatchupsCache>()
            .Verify(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
        clients.Verify(c => c.SendCoreAsync(
                nameof(PreviewGenerated),
                It.IsAny<object?[]>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Consume_StillBroadcasts_WhenEvictionThrows()
    {
        var contestId = Guid.NewGuid();
        DataContext.PickemGroupMatchups.Add(Matchup(Guid.NewGuid(), contestId, week: 3));
        await DataContext.SaveChangesAsync();

        Mocker.GetMock<ILeagueWeekMatchupsCache>()
            .Setup(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var clients = WireHubContext();
        var sut = Mocker.CreateInstance<PreviewGeneratedHandler>();

        // The preview is persisted; the broadcast is not allowed to be the casualty.
        await sut.Consume(ContextFor(Message(contestId)));

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

    private static PickemGroupMatchup Matchup(Guid groupId, Guid contestId, int week) => new()
    {
        Id = Guid.NewGuid(),
        GroupId = groupId,
        ContestId = contestId,
        SeasonWeekId = Guid.NewGuid(),
        SeasonYear = 2026,
        SeasonWeek = week,
        StartDateUtc = Now.AddDays(5),
        CreatedUtc = Now,
        CreatedBy = Guid.Empty
    };

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
