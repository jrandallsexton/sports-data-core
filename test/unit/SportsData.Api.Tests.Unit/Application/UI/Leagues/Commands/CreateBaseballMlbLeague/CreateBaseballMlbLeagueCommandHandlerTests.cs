using FluentAssertions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateBaseballMlbLeague.Dtos;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

// Alias: SportsData.Api.Tests.Unit.Application.User is a sibling namespace
// that beats the User entity type in name resolution from this file.
using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CreateBaseballMlbLeague;

public class CreateBaseballMlbLeagueCommandHandlerTests : ApiTestBase<CreateBaseballMlbLeagueCommandHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;
    private readonly Mock<IProvideContests> _contestClientMock;

    public CreateBaseballMlbLeagueCommandHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);

        // Contest client backs the blackout-date guard. Default: the window has
        // games (guard passes) so non-guard tests are unaffected. Guard tests
        // override GetGameDates per case.
        _contestClientMock = new Mock<IProvideContests>();
        _contestClientMock
            .Setup(x => x.GetGameDates(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<DateOnly>>(new List<DateOnly> { new(2026, 7, 15) }));
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);

        // Fixed clock far enough in the past that the validator's
        // EndsOn-in-future rule doesn't fire for the (null EndsOn) test requests.
        var dateTimeProviderMock = Mocker.GetMock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(x => x.UtcNow()).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Use the real validator so Name/enum/date-window assertions exercise the
        // production rules rather than AutoMocker's default IsValid=true stub.
        Mocker.Use<IValidator<CreateBaseballMlbLeagueRequest>>(
            new CreateBaseballMlbLeagueRequestValidator(dateTimeProviderMock.Object));
    }

    private CreateBaseballMlbLeagueRequest BuildValidRequest() => new()
    {
        Name = "My MLB League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        DivisionSlugs = new List<string> { "american-league-east" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    [Fact]
    public async Task ShouldFail_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.Name = null!;

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldCreateLeague_AndPublishCreatedEvent_OnHappyPath()
    {
        // Mirrors the NCAA/NFL happy-path tests against the MLB handler's
        // SportMode/LeagueMode. Pins the new MaxUsers = int.MaxValue
        // behavior the base introduced.
        const int SeasonYear = 2026;
        var currentUserId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        request.DivisionSlugs = ["american-league-east"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [divisionId] = "american-league-east" });

        var synthetic = new UserEntity
        {
            Username = "test_user_1",
            Id = Guid.NewGuid(),
            FirebaseUid = "synthetic",
            Email = "synthetic@sportdeets.test",
            SignInProvider = "synthetic",
            DisplayName = "Synthetic",
            IsSynthetic = true,
            CreatedBy = Guid.Empty,
        };
        await DataContext.Users.AddAsync(synthetic);
        await DataContext.SaveChangesAsync();

        var eventBusMock = Mocker.GetMock<IEventBus>();

        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeTrue();
        var leagueId = ((Success<Guid>)result).Value;

        var saved = await DataContext.PickemGroups
            .Include(g => g.Conferences)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == leagueId);

        saved.Should().NotBeNull();
        saved!.Sport.Should().Be(Sport.BaseballMlb);
        saved.League.Should().Be(League.MLB);
        saved.Name.Should().Be("My MLB League");
        saved.MaxUsers.Should().Be(int.MaxValue);
        saved.CommissionerUserId.Should().Be(currentUserId);

        saved.Conferences.Should().ContainSingle()
            .Which.Should().Match<PickemGroupConference>(c =>
                c.ConferenceId == divisionId && c.ConferenceSlug == "american-league-east");

        saved.Members.Should().HaveCount(2);
        saved.Members.Should().Contain(m =>
            m.UserId == currentUserId && m.Role == LeagueRole.Commissioner);
        saved.Members.Should().Contain(m =>
            m.UserId == synthetic.Id && m.Role == LeagueRole.Member);

        eventBusMock.Verify(
            x => x.Publish(
                It.Is<PickemGroupCreated>(e => e.GroupId == leagueId && e.Sport == Sport.BaseballMlb),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldFail_WhenAnyDivisionSlugNotResolved()
    {
        var request = BuildValidRequest();
        var currentUserId = Guid.NewGuid();
        var currentYear = DateTime.UtcNow.Year;

        var slugToGuid = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "american-league-east",
            [Guid.NewGuid()] = "american-league-central"
        };

        request.DivisionSlugs = ["american-league-east", "american-league-central", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(currentYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenWindowedRangeHasNoGames_AndPublishNothing()
    {
        // The reported bug: a windowed league (here a single day) whose range has
        // no games must be rejected, and no PickemGroupCreated should be published.
        var request = BuildValidRequest();
        request.SeasonYear = 2026;
        request.StartsOn = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);
        request.EndsOn = new DateTime(2026, 7, 14, 0, 0, 0, DateTimeKind.Utc);

        _contestClientMock
            .Setup(x => x.GetGameDates(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<DateOnly>>(new List<DateOnly>()));

        var eventBusMock = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        eventBusMock.Verify(
            x => x.Publish(It.IsAny<PickemGroupCreated>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ShouldCreateLeague_WhenWindowedRangeHasGames()
    {
        // A windowed league whose range DOES contain games proceeds normally.
        const int SeasonYear = 2026;
        var currentUserId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        request.DivisionSlugs = ["american-league-east"];
        request.StartsOn = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc);
        request.EndsOn = new DateTime(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

        _contestClientMock
            .Setup(x => x.GetGameDates(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<DateOnly>>(new List<DateOnly> { new(2026, 7, 15), new(2026, 7, 16) }));

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [divisionId] = "american-league-east" });

        var synthetic = new UserEntity
        {
            Username = "test_user_2",
            Id = Guid.NewGuid(),
            FirebaseUid = "synthetic",
            Email = "synthetic2@sportdeets.test",
            SignInProvider = "synthetic",
            DisplayName = "Synthetic",
            IsSynthetic = true,
            CreatedBy = Guid.Empty,
        };
        await DataContext.Users.AddAsync(synthetic);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<CreateBaseballMlbLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeTrue();
    }
}
