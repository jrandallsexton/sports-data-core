using FluentAssertions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Common.Enums;
using Moq;

using SportsData.Api.Application.UI.Leagues.Commands.CreatePlayerLeague;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues;

/// <summary>
/// Player Pick'em league creation: the alpha admin gate, the GroupType
/// stamp, and validation. The weeks-without-matchups behavior lives in
/// MatchupScheduleProcessor and is covered by its tests.
/// </summary>
public class CreatePlayerLeagueCommandHandlerTests : ApiTestBase<CreatePlayerLeagueCommandHandler>
{
    public CreatePlayerLeagueCommandHandlerTests()
    {
        // Real validator — an auto-mocked IValidator returns a null
        // ValidationResult and NREs before the code under test runs.
        Mocker.Use<IValidator<CreatePlayerLeagueCommand>>(new CreatePlayerLeagueCommandValidator());

        // Blackout guard dependency: windowed requests check game dates.
        // The auto-mocked client would return a NULL Result and NRE.
        var contestClient = new Mock<IProvideContests>();
        contestClient
            .Setup(x => x.GetGameDates(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<DateOnly>>([new DateOnly(2026, 8, 27)]));
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(contestClient.Object);
    }

    private async Task<Guid> SeedUserAsync(bool isAdmin)
    {
        var userId = Guid.NewGuid();
        DataContext.Users.Add(new UserEntity
        {
            Id = userId,
            FirebaseUid = $"fb-{userId:N}",
            Email = "op@sportdeets.com",
            SignInProvider = "password",
            DisplayName = "Operator",
            Username = $"op{userId:N}"[..12],
            IsAdmin = isAdmin,
        });
        await DataContext.SaveChangesAsync();
        return userId;
    }

    private static CreatePlayerLeagueCommand ValidRequest() => new()
    {
        Sport = "FootballNfl",
        Name = "NFL Rosters",
        IsPublic = true,
    };

    [Fact]
    public async Task NonAdmin_IsForbidden()
    {
        var userId = await SeedUserAsync(isAdmin: false);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(ValidRequest(), userId);

        result.Status.Should().Be(ResultStatus.Forbid);
        (await DataContext.PickemGroups.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Admin_CreatesPlayerPickemLeague_WithCommissionerMembership()
    {
        var userId = await SeedUserAsync(isAdmin: true);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var result = await handler.ExecuteAsync(ValidRequest(), userId);

        result.IsSuccess.Should().BeTrue();
        var group = await DataContext.PickemGroups
            .Include(g => g.Members)
            .SingleAsync(g => g.Id == result.Value);
        group.GroupType.Should().Be(GroupType.PlayerPickem);
        group.Sport.Should().Be(Sport.FootballNfl);
        group.League.Should().Be(League.NFL);
        group.CommissionerUserId.Should().Be(userId);
        group.Members.Should().ContainSingle(m => m.UserId == userId && m.Role == LeagueRole.Commissioner);
    }

    [Theory]
    [InlineData("BaseballMlb")] // player pick'em is football-only
    [InlineData("")]
    public async Task UnsupportedSport_FailsValidation(string sport)
    {
        var userId = await SeedUserAsync(isAdmin: true);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var request = ValidRequest();
        request.Sport = sport;
        var result = await handler.ExecuteAsync(request, userId);

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task DateOnlyBounds_PersistAsUtc()
    {
        // JSON date-only values deserialize Kind=Unspecified — Npgsql
        // rejects those on timestamptz (the E2E 500). The request's
        // Effective* properties stamp UTC; assert the entity carries it.
        var userId = await SeedUserAsync(isAdmin: true);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var request = ValidRequest();
        request.StartsOn = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Unspecified);
        request.EndsOn = new DateTime(2026, 8, 27, 0, 0, 0, DateTimeKind.Unspecified);
        var result = await handler.ExecuteAsync(request, userId);

        result.IsSuccess.Should().BeTrue();
        var group = await DataContext.PickemGroups.SingleAsync(g => g.Id == result.Value);
        group.StartsOn!.Value.Kind.Should().Be(DateTimeKind.Utc);
        group.EndsOn!.Value.Kind.Should().Be(DateTimeKind.Utc);
        // Inclusive end: midnight input becomes end-of-day.
        group.EndsOn.Value.Hour.Should().Be(23);
    }

    [Fact]
    public async Task LocalKindBounds_AreConvertedToUtc_NotRelabeled()
    {
        // A Local wall-clock relabeled as UTC would silently shift the
        // instant by the machine's offset — Local must be CONVERTED.
        var userId = await SeedUserAsync(isAdmin: true);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var localStart = new DateTime(2026, 8, 27, 9, 30, 0, DateTimeKind.Local);
        var request = ValidRequest();
        request.StartsOn = localStart;
        request.EndsOn = new DateTime(2026, 8, 28, 18, 0, 0, DateTimeKind.Local);
        var result = await handler.ExecuteAsync(request, userId);

        result.IsSuccess.Should().BeTrue();
        var group = await DataContext.PickemGroups.SingleAsync(g => g.Id == result.Value);
        group.StartsOn!.Value.Kind.Should().Be(DateTimeKind.Utc);
        group.StartsOn.Value.Should().Be(localStart.ToUniversalTime());
        group.EndsOn!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public async Task LocalStart_SameDayAsDateOnlyEnd_PassesRangeValidation()
    {
        // The range rule compares NORMALIZED bounds. A local mid-day start
        // with a same-day date-only end must validate regardless of the
        // machine's offset: effective start (local noon -> UTC, at most
        // +/-14h) always precedes the effective end (next-day end-of-day
        // expansion of the date-only value). A raw-vs-effective comparison
        // could flip this by server timezone.
        var userId = await SeedUserAsync(isAdmin: true);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var request = ValidRequest();
        request.StartsOn = new DateTime(2026, 8, 27, 12, 0, 0, DateTimeKind.Local);
        request.EndsOn = new DateTime(2026, 8, 28, 0, 0, 0, DateTimeKind.Unspecified);
        var result = await handler.ExecuteAsync(request, userId);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MaxValueEndsOn_FailsValidation_InsteadOfThrowing()
    {
        // DateTime.MaxValue.Date is midnight — without the guard the
        // end-of-day AddDays(1) would overflow inside the validator.
        var userId = await SeedUserAsync(isAdmin: true);
        var handler = Mocker.CreateInstance<CreatePlayerLeagueCommandHandler>();

        var request = ValidRequest();
        request.EndsOn = DateTime.MaxValue.Date;
        var result = await handler.ExecuteAsync(request, userId);

        result.Status.Should().Be(ResultStatus.Validation);
    }
}
