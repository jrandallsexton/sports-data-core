using FluentAssertions;

using Microsoft.Extensions.Options;

using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.SendLeagueInvite;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Api.Infrastructure.Notifications;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.SendLeagueInvite;

public class SendLeagueInviteCommandHandlerTests : ApiTestBase<SendLeagueInviteCommandHandler>
{
    public SendLeagueInviteCommandHandlerTests()
    {
        Mocker.Use<IOptions<NotificationConfig>>(Options.Create(new NotificationConfig
        {
            Email = new NotificationConfig.EmailConfig
            {
                ApiKey = "key",
                FromEmail = "no-reply@sportdeets.com",
                TemplateIdInvitation = "tmpl",
                UrlBase = "www.sportdeets.com"
            }
        }));
    }

    private async Task<PickemGroup> SeedLeagueAsync()
    {
        var league = new PickemGroup
        {
            Id = Guid.NewGuid(),
            Name = "Saturday Smackdown",
            Sport = Sport.FootballNcaa,
            League = SportsData.Api.Application.Common.Enums.League.NCAAF
        };
        await DataContext.PickemGroups.AddAsync(league);
        await DataContext.SaveChangesAsync();
        return league;
    }

    private async Task<SportsData.Api.Infrastructure.Data.Entities.User> SeedUserAsync(string email)
    {
        var user = new SportsData.Api.Infrastructure.Data.Entities.User
        {
            Id = Guid.NewGuid(),
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = email,
            SignInProvider = "password",
            DisplayName = "Invitee",
            Username = email.Split('@')[0]
        };
        await DataContext.Users.AddAsync(user);
        await DataContext.SaveChangesAsync();
        return user;
    }

    private async Task AddMemberAsync(Guid leagueId, Guid userId)
    {
        await DataContext.PickemGroupMembers.AddAsync(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = leagueId,
            UserId = userId
        });
        await DataContext.SaveChangesAsync();
    }

    private void VerifyPublish(Times times) =>
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(It.IsAny<UserInvitedToPickemGroup>(), It.IsAny<CancellationToken>()), times);

    private void VerifyEmailSent(Times times) =>
        Mocker.GetMock<INotificationService>().Verify(
            n => n.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()), times);

    [Fact]
    public async Task ExecuteAsync_ReturnsNotFound_WhenLeagueMissing()
    {
        var handler = Mocker.CreateInstance<SendLeagueInviteCommandHandler>();

        var result = await handler.ExecuteAsync(new SendLeagueInviteCommand
        {
            LeagueId = Guid.NewGuid(),
            Email = "nobody@x.com",
            InvitedByUserId = Guid.NewGuid()
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        VerifyPublish(Times.Never());
    }

    [Fact]
    public async Task ExecuteAsync_UnregisteredEmail_SendsEmailOnly_NoEvent()
    {
        var league = await SeedLeagueAsync();
        var invitedBy = Guid.NewGuid();
        await AddMemberAsync(league.Id, invitedBy);
        var handler = Mocker.CreateInstance<SendLeagueInviteCommandHandler>();

        var result = await handler.ExecuteAsync(new SendLeagueInviteCommand
        {
            LeagueId = league.Id,
            Email = "stranger@x.com",
            InvitedByUserId = invitedBy
        });

        result.IsSuccess.Should().BeTrue();
        VerifyEmailSent(Times.Once());
        VerifyPublish(Times.Never());
    }

    [Fact]
    public async Task ExecuteAsync_RegisteredNonMember_PublishesInviteEvent()
    {
        var league = await SeedLeagueAsync();
        var invitee = await SeedUserAsync("friend@x.com");
        var invitedBy = Guid.NewGuid();
        await AddMemberAsync(league.Id, invitedBy);
        var handler = Mocker.CreateInstance<SendLeagueInviteCommandHandler>();

        var result = await handler.ExecuteAsync(new SendLeagueInviteCommand
        {
            LeagueId = league.Id,
            Email = "friend@x.com",
            InvitedByUserId = invitedBy
        });

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            b => b.Publish(
                It.Is<UserInvitedToPickemGroup>(e =>
                    e.InviteeUserId == invitee.Id &&
                    e.GroupId == league.Id &&
                    e.LeagueName == league.Name &&
                    e.InvitedByUserId == invitedBy &&
                    e.Sport == league.Sport &&
                    e.SeasonYear == null),
                It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task ExecuteAsync_RegisteredButAlreadyMember_NoEvent()
    {
        var league = await SeedLeagueAsync();
        var invitee = await SeedUserAsync("member@x.com");
        await AddMemberAsync(league.Id, invitee.Id);
        var invitedBy = Guid.NewGuid();
        await AddMemberAsync(league.Id, invitedBy);

        var handler = Mocker.CreateInstance<SendLeagueInviteCommandHandler>();

        var result = await handler.ExecuteAsync(new SendLeagueInviteCommand
        {
            LeagueId = league.Id,
            Email = "member@x.com",
            InvitedByUserId = invitedBy
        });

        result.IsSuccess.Should().BeTrue();
        VerifyPublish(Times.Never());
    }

    [Fact]
    public async Task ExecuteAsync_Forbids_WhenInviterNotAMember()
    {
        var league = await SeedLeagueAsync();
        var handler = Mocker.CreateInstance<SendLeagueInviteCommandHandler>();

        var result = await handler.ExecuteAsync(new SendLeagueInviteCommand
        {
            LeagueId = league.Id,
            Email = "stranger@x.com",
            InvitedByUserId = Guid.NewGuid() // not a member of the league
        });

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
        VerifyEmailSent(Times.Never());
        VerifyPublish(Times.Never());
    }
}
