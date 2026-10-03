using System.Linq.Expressions;

using MassTransit;

using Moq;

using SportsData.Api.Application.Contests.Consumers;
using SportsData.Api.Application.Matchups.Jobs.ApplyMatchupOdds;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Processing;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Contests.Consumers;

/// <summary>
/// Both odds consumers are thin shims: enqueue the matchup-odds job only when
/// the event carries the displayed row. Everything else (another book's
/// change, a pod on the prior shape) must enqueue nothing.
/// </summary>
public class ContestOddsConsumersTests : ApiTestBase<ContestOddsUpdatedConsumer>
{
    private readonly Guid _contestId = Guid.NewGuid();
    private readonly Guid _correlationId = Guid.NewGuid();
    private readonly List<ApplyMatchupOddsCommand> _enqueued = [];

    private static readonly DisplayedContestOdds Displayed = new()
    {
        ProviderId = "58",
        Details = "HOME -3.5",
        Spread = -3.5m,
        OverUnder = 52.5m,
        AwayMoneyLine = 150,
        HomeMoneyLine = -175
    };

    public ContestOddsConsumersTests()
    {
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue(It.IsAny<Expression<Func<IApplyMatchupOdds, Task>>>()))
            .Callback<Expression<Func<IApplyMatchupOdds, Task>>>(e =>
            {
                var call = (MethodCallExpression)e.Body;
                _enqueued.Add((ApplyMatchupOddsCommand)Expression.Lambda(call.Arguments[0]).Compile().DynamicInvoke()!);
            });
    }

    private ContestOddsUpdated Updated(DisplayedContestOdds? displayed) => new(
        _contestId, "ContestOddsUpdated", "58", "ESPN BET", -3m, -3.5m, 52m, 52.5m,
        null, Sport.FootballNcaa, 2026, _correlationId, Guid.NewGuid(), DisplayedOdds: displayed);

    private ContestOddsCreated Created(DisplayedContestOdds? displayed) => new(
        _contestId, null, Sport.FootballNcaa, 2026, _correlationId, Guid.NewGuid(), DisplayedOdds: displayed);

    private void AssertEnqueuedTheDisplayedOdds(DateTime expectedAsOfUtc)
    {
        var cmd = Assert.Single(_enqueued);
        // The event's CreatedUtc is the odds version the job orders by.
        Assert.Equal(expectedAsOfUtc, cmd.AsOfUtc);
        Assert.Equal(_contestId, cmd.ContestId);
        Assert.Equal(Sport.FootballNcaa, cmd.Sport);
        Assert.Equal(_correlationId, cmd.CorrelationId);
        Assert.Equal(Displayed, cmd.Odds);
    }

    [Fact]
    public async Task Updated_WithDisplayedOdds_EnqueuesTheMatchupOddsJob()
    {
        var msg = Updated(Displayed);
        await Mocker.CreateInstance<ContestOddsUpdatedConsumer>()
            .Consume(Mock.Of<ConsumeContext<ContestOddsUpdated>>(c => c.Message == msg));

        AssertEnqueuedTheDisplayedOdds(msg.CreatedUtc);
    }

    [Fact]
    public async Task Updated_WithoutDisplayedOdds_EnqueuesNothing()
    {
        // Another book changed (or a pod on the prior shape): nothing to apply.
        await Mocker.CreateInstance<ContestOddsUpdatedConsumer>()
            .Consume(Mock.Of<ConsumeContext<ContestOddsUpdated>>(c => c.Message == Updated(null)));

        Assert.Empty(_enqueued);
    }

    [Fact]
    public async Task Created_WithDisplayedOdds_EnqueuesTheMatchupOddsJob()
    {
        var msg = Created(Displayed);
        await Mocker.CreateInstance<ContestOddsCreatedConsumer>()
            .Consume(Mock.Of<ConsumeContext<ContestOddsCreated>>(c => c.Message == msg));

        AssertEnqueuedTheDisplayedOdds(msg.CreatedUtc);
    }

    [Fact]
    public async Task Created_WithoutDisplayedOdds_EnqueuesNothing()
    {
        await Mocker.CreateInstance<ContestOddsCreatedConsumer>()
            .Consume(Mock.Of<ConsumeContext<ContestOddsCreated>>(c => c.Message == Created(null)));

        Assert.Empty(_enqueued);
    }
}
