using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Application.Consumers;
using SportsData.Notification.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Notification.Tests.Unit.Application.Consumers;

public class PickemGroupCreatedConsumerTests : NotificationTestBase<PickemGroupCreatedConsumer>
{
    private static readonly DateTime FixedNow = new(2026, 6, 28, 12, 0, 0, DateTimeKind.Utc);

    public PickemGroupCreatedConsumerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    private static ConsumeContext<PickemGroupCreated> ContextFor(PickemGroupCreated msg)
    {
        var ctx = new Mock<ConsumeContext<PickemGroupCreated>>();
        ctx.SetupGet(x => x.Message).Returns(msg);
        ctx.SetupGet(x => x.CancellationToken).Returns(CancellationToken.None);
        return ctx.Object;
    }

    private static PickemGroupCreated Msg(
        Guid groupId,
        string name = "Test League",
        string pickType = LeaguePickType.AgainstTheSpread,
        Guid? commissionerId = null)
        => new(groupId, name, commissionerId ?? Guid.NewGuid(), pickType,
            null, Sport.FootballNcaa, 2026, Guid.NewGuid(), Guid.NewGuid());

    [Fact]
    public async Task Consume_InsertsProjection_WithPickType()
    {
        var groupId = Guid.NewGuid();
        var commissionerId = Guid.NewGuid();

        var sut = Mocker.CreateInstance<PickemGroupCreatedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, "ATS League", LeaguePickType.AgainstTheSpread, commissionerId)));

        var row = await DataContext.PickemGroups.SingleAsync();
        row.Id.Should().Be(groupId);
        row.Name.Should().Be("ATS League");
        row.Sport.Should().Be(Sport.FootballNcaa);
        row.CommissionerUserId.Should().Be(commissionerId);
        row.PickType.Should().Be(LeaguePickType.AgainstTheSpread);
        row.CreatedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_UpdatesPickType_WhenChanged()
    {
        var groupId = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            CommissionerUserId = Guid.NewGuid(),
            PickType = LeaguePickType.StraightUp,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickemGroupCreatedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, "Test League", LeaguePickType.OverUnder)));

        var row = await DataContext.PickemGroups.SingleAsync();
        row.PickType.Should().Be(LeaguePickType.OverUnder);
        row.ModifiedUtc.Should().Be(FixedNow);
    }

    [Fact]
    public async Task Consume_IsNoOp_WhenUnchanged()
    {
        var groupId = Guid.NewGuid();
        var commissionerId = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = "Test League",
            Sport = Sport.FootballNcaa,
            CommissionerUserId = commissionerId,
            PickType = LeaguePickType.AgainstTheSpread,
            CreatedUtc = FixedNow.AddDays(-1),
            CreatedBy = Guid.NewGuid()
        });
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickemGroupCreatedConsumer>();
        await sut.Consume(ContextFor(Msg(groupId, "Test League", LeaguePickType.AgainstTheSpread, commissionerId)));

        var row = await DataContext.PickemGroups.SingleAsync();
        row.ModifiedUtc.Should().BeNull("an unchanged redelivery must not bump ModifiedUtc");
    }
}
