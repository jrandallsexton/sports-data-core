using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class UserPickMadeConsumerTests : NotificationTestBase<UserPickMadeConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    public UserPickMadeConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    private static ConsumeContext<UserPickMade> ContextFor(UserPickMade msg)
    {
        var ctx = new Mock<ConsumeContext<UserPickMade>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static UserPickMade Msg(Guid userId, Guid contestId, Guid groupId)
        => new(userId, contestId, groupId, Sport.FootballNcaa, 2026, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Consume_InsertsRow_WhenAbsent()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var sut = Mocker.CreateInstance<UserPickMadeConsumer>();
        await sut.Consume(ContextFor(Msg(userId, contestId, groupId)));

        var row = await DataContext.UserPicks.SingleAsync();
        row.UserId.Should().Be(userId);
        row.ContestId.Should().Be(contestId);
        row.PickemGroupId.Should().Be(groupId);
        row.CreatedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_IsNoOp_WhenAlreadyProjected()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        DataContext.UserPicks.Add(new UserPick
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            ContestId = contestId,
            PickemGroupId = groupId,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<UserPickMadeConsumer>();
        await sut.Consume(ContextFor(Msg(userId, contestId, groupId)));

        // Still exactly one row — re-publish of the same pick is a no-op.
        (await DataContext.UserPicks.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Consume_InsertsSeparateRows_ForSameContestDifferentLeagues()
    {
        var userId = Guid.NewGuid();
        var contestId = Guid.NewGuid();

        var sut = Mocker.CreateInstance<UserPickMadeConsumer>();
        await sut.Consume(ContextFor(Msg(userId, contestId, Guid.NewGuid())));
        await sut.Consume(ContextFor(Msg(userId, contestId, Guid.NewGuid())));

        (await DataContext.UserPicks.CountAsync()).Should().Be(2);
    }
}
