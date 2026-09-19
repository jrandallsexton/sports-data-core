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
    /// <summary>
    /// The APPROVED preview, by id — not "the latest non-rejected one".
    /// Approving an older preview while a newer one exists must persist the
    /// prediction the operator approved, not the newer one.
    /// </summary>
    [Fact]
    public async Task Consume_RederivesFromTheApprovedPreviewId_AndEvictsWhenSomethingChanged()
    {
        var contestId = Guid.NewGuid();
        var previewId = Guid.NewGuid();
        Mocker.GetMock<IStatBotPickWriter>()
            .Setup(w => w.UpsertForContestAsync(contestId, previewId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(2);

        await Mocker.CreateInstance<MatchupPreviewApprovedHandler>().Consume(ContextFor(contestId, previewId));

        Mocker.GetMock<IStatBotPickWriter>()
            .Verify(w => w.UpsertForContestAsync(contestId, previewId, It.IsAny<CancellationToken>()), Times.Once);
        Mocker.GetMock<IStatBotPickWriter>()
            .Verify(w => w.UpsertForContestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never,
                "the contest-only overload would resolve the NEWEST preview, not the approved one");
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(contestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Consume_NothingChanged_DoesNotEvict()
    {
        var contestId = Guid.NewGuid();
        Mocker.GetMock<IStatBotPickWriter>()
            .Setup(w => w.UpsertForContestAsync(contestId, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        await Mocker.CreateInstance<MatchupPreviewApprovedHandler>().Consume(ContextFor(contestId));

        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>().Verify(i => i.EvictForContestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ConsumeContext<MatchupPreviewApproved> ContextFor(Guid contestId, Guid? previewId = null)
    {
        var message = new MatchupPreviewApproved(
            MatchupPreviewId: previewId ?? Guid.NewGuid(),
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
