using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.PickemGroups;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.PickemGroups;

/// <summary>
/// The handler is now a thin fan-out point: existence-check + enqueue.
/// Per-league bootstrap logic moved into
/// <see cref="BootstrapLeagueMatchupsProcessor"/>. Tests pin two contracts:
///   1. Missing group is permanent — log + return, no enqueue, no throw.
///   2. Valid group → exactly one BootstrapLeagueMatchupsCommand enqueued,
///      carrying through the event's CorrelationId.
/// </summary>
public class PickemGroupCreatedHandlerTests : ApiTestBase<PickemGroupCreatedHandler>
{
    private readonly Mock<IProvideBackgroundJobs> _backgroundJobsMock;

    public PickemGroupCreatedHandlerTests()
    {
        _backgroundJobsMock = Mocker.GetMock<IProvideBackgroundJobs>();
    }

    [Fact]
    public async Task GroupNotFound_LogsAndReturns_DoesNotThrow_NoEnqueue()
    {
        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            Guid.NewGuid(), null, Sport.BaseballMlb, 2026, Guid.NewGuid(), Guid.NewGuid()));

        var act = async () => await sut.Consume(context);

        await act.Should().NotThrowAsync();
        _backgroundJobsMock.Verify(
            x => x.Enqueue<IBootstrapLeagueMatchups>(It.IsAny<Expression<Func<IBootstrapLeagueMatchups, Task>>>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidGroup_EnqueuesBootstrap_WithEventCorrelationId()
    {
        var groupId = await SeedGroup();
        var correlationId = Guid.NewGuid();

        BootstrapLeagueMatchupsCommand? captured = null;
        _backgroundJobsMock
            .Setup(x => x.Enqueue<IBootstrapLeagueMatchups>(
                It.IsAny<Expression<Func<IBootstrapLeagueMatchups, Task>>>()))
            .Callback<Expression<Func<IBootstrapLeagueMatchups, Task>>>(expr =>
            {
                captured = ExtractCommand(expr);
            })
            .Returns("job-id");

        var sut = Mocker.CreateInstance<PickemGroupCreatedHandler>();
        var context = ConsumeContextFor(new PickemGroupCreated(
            groupId, null, Sport.BaseballMlb, 2026, correlationId, Guid.NewGuid()));

        await sut.Consume(context);

        _backgroundJobsMock.Verify(
            x => x.Enqueue<IBootstrapLeagueMatchups>(It.IsAny<Expression<Func<IBootstrapLeagueMatchups, Task>>>()),
            Times.Once);

        captured.Should().NotBeNull();
        captured!.GroupId.Should().Be(groupId);
        captured.CorrelationId.Should().Be(correlationId);
    }

    private async Task<Guid> SeedGroup()
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Sport = Sport.BaseballMlb,
            League = League.MLB,
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private static ConsumeContext<PickemGroupCreated> ConsumeContextFor(PickemGroupCreated message) =>
        Mock.Of<ConsumeContext<PickemGroupCreated>>(ctx => ctx.Message == message);

    private static BootstrapLeagueMatchupsCommand? ExtractCommand(
        Expression<Func<IBootstrapLeagueMatchups, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        var arg = call.Arguments.FirstOrDefault();
        if (arg is null) return null;

        var lambda = Expression.Lambda<Func<BootstrapLeagueMatchupsCommand>>(arg).Compile();
        return lambda.Invoke();
    }
}
