using MassTransit;

using Microsoft.AspNetCore.SignalR;

using Moq;

using SportsData.Api.Application.Plays.Consumers;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Plays.Consumers;

public class PlayAnalysisGeneratedConsumerTests : ApiTestBase<PlayAnalysisGeneratedConsumer>
{
    [Fact]
    public async Task Consume_BroadcastsTheEventAsPlayAnalysis()
    {
        var proxy = new Mock<IClientProxy>();
        var hubClients = new Mock<IHubClients>();
        hubClients.Setup(c => c.All).Returns(proxy.Object);
        Mocker.GetMock<IHubContext<NotificationHub>>().Setup(x => x.Clients).Returns(hubClients.Object);

        var evt = new PlayAnalysisGenerated(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new PlayAnalysisItem(Guid.NewGuid(), "SeasonCount", "T. Cooley's second interception of the season.")],
            null,
            Sport.FootballNcaa,
            2026,
            Guid.NewGuid(),
            Guid.NewGuid());
        var context = new Mock<ConsumeContext<PlayAnalysisGenerated>>();
        context.Setup(x => x.Message).Returns(evt);

        await Mocker.CreateInstance<PlayAnalysisGeneratedConsumer>().Consume(context.Object);

        proxy.Verify(x => x.SendCoreAsync(
                "PlayAnalysis",
                It.Is<object?[]>(args => args.Length == 1 && ReferenceEquals(args[0], evt)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
