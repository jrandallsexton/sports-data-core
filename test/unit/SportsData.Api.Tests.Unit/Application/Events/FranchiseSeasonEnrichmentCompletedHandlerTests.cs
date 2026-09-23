using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Api.Application.Events;
using SportsData.Api.Application.Processors;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Franchise;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Events
{
    /// <summary>
    /// The card reads the snapshot and never derives, so the consumer must run
    /// the record audit (which corrects AND evicts), scoped to the contests the
    /// event names, and must do nothing at all when it names none.
    /// </summary>
    public class FranchiseSeasonEnrichmentCompletedHandlerTests : ApiTestBase<FranchiseSeasonEnrichmentCompletedHandler>
    {
        private static FranchiseSeasonEnrichmentCompleted Message(IReadOnlyList<Guid>? contestIds) =>
            new(
                FranchiseSeasonId: Guid.NewGuid(),
                Ref: null,
                Sport: Sport.FootballNcaa,
                SeasonYear: 2026,
                CorrelationId: Guid.NewGuid(),
                CausationId: Guid.NewGuid(),
                ContestIds: contestIds);

        private static ConsumeContext<FranchiseSeasonEnrichmentCompleted> Context(FranchiseSeasonEnrichmentCompleted message) =>
            Mock.Of<ConsumeContext<FranchiseSeasonEnrichmentCompleted>>(ctx => ctx.Message == message);

        [Fact]
        public async Task Consume_AuditsRecordsForExactlyTheContestsTheEventNames_InTheEventsSport()
        {
            var ids = new List<Guid> { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var auditor = Mocker.GetMock<IAuditMatchupRecords>();
            auditor
                .Setup(x => x.Process(It.IsAny<MatchupRecordAuditByContestsCommand>()))
                .ReturnsAsync(new MatchupRecordAuditResult(2, 1, 0));

            var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentCompletedHandler>();
            await sut.Consume(Context(Message(ids)));

            auditor.Verify(x => x.Process(It.Is<MatchupRecordAuditByContestsCommand>(c =>
                c.Sport == Sport.FootballNcaa &&
                c.ContestIds.SequenceEqual(ids))), Times.Once);
            // Never the season-wide form: that is the operator's repair lever,
            // and running it per team would be ~800 season sweeps a week.
            auditor.Verify(x => x.Process(It.IsAny<MatchupRecordAuditCommand>()), Times.Never);
        }

        [Fact]
        public async Task Consume_WithNoContestIds_DoesNothing()
        {
            // A pre-upgrade Producer publishes without the list; a team with
            // no contests publishes an empty one. Neither can touch a card.
            var auditor = Mocker.GetMock<IAuditMatchupRecords>();
            var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentCompletedHandler>();

            await sut.Consume(Context(Message(null)));
            await sut.Consume(Context(Message([])));

            auditor.Verify(x => x.Process(It.IsAny<MatchupRecordAuditByContestsCommand>()), Times.Never);
            auditor.Verify(x => x.Process(It.IsAny<MatchupRecordAuditCommand>()), Times.Never);
        }

        [Fact]
        public async Task Consume_LetsAnAuditFailurePropagate_SoTheBrokerRetries()
        {
            // The audit throws when the Producer call fails rather than report
            // work it never persisted; the consumer must not swallow that, or
            // the retry (and the correction) never happens.
            Mocker.GetMock<IAuditMatchupRecords>()
                .Setup(x => x.Process(It.IsAny<MatchupRecordAuditByContestsCommand>()))
                .ThrowsAsync(new InvalidOperationException("Producer failed"));

            var sut = Mocker.CreateInstance<FranchiseSeasonEnrichmentCompletedHandler>();
            var act = async () => await sut.Consume(Context(Message([Guid.NewGuid()])));

            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
