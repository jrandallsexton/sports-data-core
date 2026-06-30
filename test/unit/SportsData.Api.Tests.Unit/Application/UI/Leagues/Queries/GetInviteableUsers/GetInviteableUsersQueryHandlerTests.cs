using FluentAssertions;

using SportsData.Api.Application.UI.Leagues.Queries.GetInviteableUsers;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Queries.GetInviteableUsers;

public class GetInviteableUsersQueryHandlerTests : ApiTestBase<GetInviteableUsersQueryHandler>
{
    private readonly Guid _leagueId = Guid.NewGuid();
    private readonly Guid _searcherId = Guid.NewGuid();

    private async Task<Guid> SeedUserAsync(string username, string displayName, bool synthetic = false)
    {
        var id = Guid.NewGuid();
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = id,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = $"{username}@x.com",
            SignInProvider = "password",
            DisplayName = displayName,
            Username = username,
            IsSynthetic = synthetic
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    private async Task AddMemberAsync(Guid userId)
    {
        await DataContext.PickemGroupMembers.AddAsync(new PickemGroupMember
        {
            Id = Guid.NewGuid(),
            PickemGroupId = _leagueId,
            UserId = userId
        });
        await DataContext.SaveChangesAsync();
    }

    private GetInviteableUsersQuery Query(string? q) =>
        new() { LeagueId = _leagueId, RequestingUserId = _searcherId, Q = q };

    [Fact]
    public async Task Search_Forbidden_WhenRequesterNotAMember()
    {
        // Searcher is NOT a member of the league.
        await SeedUserAsync("matchme_real", "Real");
        var sut = Mocker.CreateInstance<GetInviteableUsersQueryHandler>();

        var result = await sut.ExecuteAsync(Query("matchme"));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task Search_MatchesByUsername()
    {
        await AddMemberAsync(_searcherId);
        await SeedUserAsync("jrandallsexton", "Randall");
        var sut = Mocker.CreateInstance<GetInviteableUsersQueryHandler>();

        var result = await sut.ExecuteAsync(Query("jrandall"));

        result.Value.Should().ContainSingle(u => u.Username == "jrandallsexton");
    }

    [Fact]
    public async Task Search_MatchesByDisplayName_CaseInsensitive()
    {
        await AddMemberAsync(_searcherId);
        await SeedUserAsync("handle1", "YouWillAllLose");
        var sut = Mocker.CreateInstance<GetInviteableUsersQueryHandler>();

        var result = await sut.ExecuteAsync(Query("youwill"));

        result.Value.Should().ContainSingle(u => u.Username == "handle1");
    }

    [Fact]
    public async Task Search_ExcludesSelf_Members_AndSynthetic()
    {
        await AddMemberAsync(_searcherId);

        // Self
        await DataContext.Users.AddAsync(new UserEntity
        {
            Id = _searcherId,
            FirebaseUid = Guid.NewGuid().ToString(),
            Email = "me@x.com",
            SignInProvider = "password",
            DisplayName = "Me",
            Username = "matchme_self"
        });
        await DataContext.SaveChangesAsync();

        var member = await SeedUserAsync("matchme_member", "Member");
        await AddMemberAsync(member);

        await SeedUserAsync("matchme_bot", "Bot", synthetic: true);
        await SeedUserAsync("matchme_real", "Real");

        var sut = Mocker.CreateInstance<GetInviteableUsersQueryHandler>();

        var result = await sut.ExecuteAsync(Query("matchme"));

        result.Value.Should().ContainSingle(u => u.Username == "matchme_real");
    }

    [Fact]
    public async Task Search_ShortTerm_ReturnsEmpty()
    {
        await AddMemberAsync(_searcherId);
        await SeedUserAsync("aardvark", "A");
        var sut = Mocker.CreateInstance<GetInviteableUsersQueryHandler>();

        var result = await sut.ExecuteAsync(Query("a"));

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Search_CapsAtTenResults()
    {
        await AddMemberAsync(_searcherId);
        for (var i = 0; i < 15; i++)
            await SeedUserAsync($"capper{i:00}", $"Capper {i}");

        var sut = Mocker.CreateInstance<GetInviteableUsersQueryHandler>();

        var result = await sut.ExecuteAsync(Query("capper"));

        result.Value.Should().HaveCount(10);
    }
}
