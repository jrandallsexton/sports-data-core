using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Events;
using SportsData.Producer.Application.Franchises.Commands;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Events;

public class ContestFinalizedHandlerTests : ProducerTestBase<ContestFinalizedHandler>
{
    private readonly Guid _contestId = Guid.NewGuid();
    private readonly Guid _correlationId = Guid.NewGuid();
    private readonly Guid _awayFranchiseSeasonId = Guid.NewGuid();
    private readonly Guid _homeFranchiseSeasonId = Guid.NewGuid();

    private ContestFinalized Message(Guid? away, Guid? home, int? seasonYear = 2026, Sport sport = Sport.FootballNcaa) =>
        new(
            ContestId: _contestId,
            Ref: null,
            Sport: sport,
            SeasonYear: seasonYear,
            CorrelationId: _correlationId,
            CausationId: Guid.NewGuid(),
            AwayScore: 10,
            HomeScore: 21,
            WinnerFranchiseSeasonId: home,
            AwayFranchiseSeasonId: away,
            HomeFranchiseSeasonId: home);

    private async Task ConsumeAsync(ContestFinalized message)
    {
        var context = Mock.Of<ConsumeContext<ContestFinalized>>(ctx => ctx.Message == message);
        var sut = Mocker.CreateInstance<ContestFinalizedHandler>();
        await sut.Consume(context);
    }

    private void VerifyEnqueued(Guid franchiseSeasonId, int seasonYear, Times times) =>
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(x => x.Enqueue<IEnrichFranchiseSeasons>(
            It.Is<Expression<Func<IEnrichFranchiseSeasons, Task>>>(expr =>
                EnqueueInvokesProcessWith(expr, franchiseSeasonId, seasonYear, _correlationId))),
            times);

    private void VerifyEnqueueCount(Times times) =>
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(x => x.Enqueue<IEnrichFranchiseSeasons>(
            It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>()),
            times);

    [Theory]
    [InlineData(Sport.FootballNcaa)]
    [InlineData(Sport.FootballNfl)]
    [InlineData(Sport.BaseballMlb)]
    public async Task Consume_EnqueuesRecordEnrichmentForBothParticipants_UnderTheEventsCorrelationId(Sport sport)
    {
        await ConsumeAsync(Message(_awayFranchiseSeasonId, _homeFranchiseSeasonId, sport: sport));

        VerifyEnqueued(_awayFranchiseSeasonId, 2026, Times.Once());
        VerifyEnqueued(_homeFranchiseSeasonId, 2026, Times.Once());
        VerifyEnqueueCount(Times.Exactly(2));

        // Enqueued by interface, immediately: the outbox publish of
        // ContestFinalized commits with FinalizedUtc, so no delay is needed.
        Mocker.GetMock<IProvideBackgroundJobs>().Verify(x => x.Schedule<IEnrichFranchiseSeasons>(
            It.IsAny<Expression<Func<IEnrichFranchiseSeasons, Task>>>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    [Fact]
    public async Task Consume_PriorShapeWithoutParticipants_EnqueuesNothing()
    {
        // A pod still publishing the pre-change shape during a rolling
        // deploy sends no participant ids; the weekly job covers those teams.
        await ConsumeAsync(Message(away: null, home: null));

        VerifyEnqueueCount(Times.Never());
    }

    [Fact]
    public async Task Consume_OneParticipantMissing_EnqueuesOnlyTheOther()
    {
        await ConsumeAsync(Message(away: null, home: _homeFranchiseSeasonId));

        VerifyEnqueued(_homeFranchiseSeasonId, 2026, Times.Once());
        VerifyEnqueueCount(Times.Once());
    }

    [Fact]
    public async Task Consume_EmptyGuidParticipant_IsSkipped()
    {
        await ConsumeAsync(Message(away: Guid.Empty, home: _homeFranchiseSeasonId));

        VerifyEnqueued(_homeFranchiseSeasonId, 2026, Times.Once());
        VerifyEnqueueCount(Times.Once());
    }

    [Fact]
    public async Task Consume_NoSeasonYear_EnqueuesNothing()
    {
        await ConsumeAsync(Message(_awayFranchiseSeasonId, _homeFranchiseSeasonId, seasonYear: null));

        VerifyEnqueueCount(Times.Never());
    }

    /// <summary>
    /// True iff <paramref name="expr"/> is shaped as <c>p =&gt; p.Process(cmd)</c>
    /// with an <see cref="EnrichFranchiseSeasonCommand"/> carrying the expected
    /// values. Same compile-and-eval approach as ContestCompletedHandlerTests.
    /// </summary>
    private static bool EnqueueInvokesProcessWith(
        Expression<Func<IEnrichFranchiseSeasons, Task>> expr,
        Guid expectedFranchiseSeasonId,
        int expectedSeasonYear,
        Guid expectedCorrelationId)
    {
        if (expr.Body is not MethodCallExpression call) return false;
        if (call.Method.Name != nameof(IEnrichFranchiseSeasons.Process)) return false;
        if (call.Arguments.Count != 1) return false;

        var cmd = Expression.Lambda<Func<EnrichFranchiseSeasonCommand>>(call.Arguments[0]).Compile()();
        return cmd != null
            && cmd.FranchiseSeasonId == expectedFranchiseSeasonId
            && cmd.SeasonYear == expectedSeasonYear
            && cmd.CorrelationId == expectedCorrelationId;
    }
}
