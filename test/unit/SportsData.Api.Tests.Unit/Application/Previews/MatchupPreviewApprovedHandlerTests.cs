using MassTransit;

using Moq;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Previews;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Previews;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Previews;

public class MatchupPreviewApprovedHandlerTests : ApiTestBase<MatchupPreviewApprovedHandler>
{
    [Fact]
    public async Task Consume_RederivesStatBotsPick_AndEvictsWhenSomethingChanged()
    {
        var contestId = Guid.NewGuid();
        Mocker.GetMock<IStatBotPickWriter>()
            .Setup(w => w.UpsertForContestAsync(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await Mocker.CreateInstance<MatchupPreviewApprovedHandler>().Consume(ContextFor(contestId));

        Mocker.GetMock<IStatBotPickWriter>().Verify(w => w.UpsertForContestAsync(contestId, It.IsAny<CancellationToken>()), Times.Once);
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>().Verify(i => i.EvictForContestAsync(contestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_NothingChanged_DoesNotEvict()
    {
        var contestId = Guid.NewGuid();
        Mocker.GetMock<IStatBotPickWriter>()
            .Setup(w => w.UpsertForContestAsync(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await Mocker.CreateInstance<MatchupPreviewApprovedHandler>().Consume(ContextFor(contestId));

        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>().Verify(i => i.EvictForContestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ConsumeContext<MatchupPreviewApproved> ContextFor(Guid contestId)
    {
        var message = new MatchupPreviewApproved(
            MatchupPreviewId: Guid.NewGuid(),
            ContestId: contestId,
            Ref: null,
            Sport: Sport.FootballNcaa,
            SeasonYear: 2026,
            CorrelationId: Guid.NewGuid(),
            CausationId: Guid.NewGuid());
        return Mock.Of<ConsumeContext<MatchupPreviewApproved>>(ctx =>
            ctx.Message == message && ctx.CancellationToken == CancellationToken.None);
    }
}
