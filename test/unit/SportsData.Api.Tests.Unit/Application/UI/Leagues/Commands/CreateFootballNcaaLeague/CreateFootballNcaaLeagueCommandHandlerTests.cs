using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNcaaLeague.Dtos;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

// Alias: SportsData.Api.Tests.Unit.Application.User is a sibling namespace
// that beats the User entity type in name resolution from this file.
using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CreateFootballNcaaLeague;

public class CreateFootballNcaaLeagueCommandHandlerTests : ApiTestBase<CreateFootballNcaaLeagueCommandHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public CreateFootballNcaaLeagueCommandHandlerTests()
    {
        _franchiseClientFactoryMock = Mocker.GetMock<IFranchiseClientFactory>();
        _franchiseClientMock = new Mock<IProvideFranchises>();
        _franchiseClientFactoryMock
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_franchiseClientMock.Object);

        // Fixed clock for the validator's EndsOn-in-future rule. Test
        // requests use a null EndsOn so the rule is a no-op; setup is just
        // to satisfy the constructor dependency.
        var dateTimeProviderMock = Mocker.GetMock<IDateTimeProvider>();
        dateTimeProviderMock.Setup(x => x.UtcNow()).Returns(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        // Use the real validator so Name/enum/date-window assertions exercise the
        // production rules rather than AutoMocker's default IsValid=true stub.
        Mocker.Use<FluentValidation.IValidator<CreateFootballNcaaLeagueRequest>>(
            new CreateFootballNcaaLeagueRequestValidator(dateTimeProviderMock.Object));
    }

    private CreateFootballNcaaLeagueRequest BuildValidRequest() => new()
    {
        Name = "My League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        ConferenceSlugs = new List<string> { "sec" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    [Fact]
    public async Task ShouldFail_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.Name = null!;

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenTiebreakerTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.TiebreakerType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldFail_WhenTiebreakerTiePolicyIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.RankingFilter = "AP_TOP_25";
        request.TiebreakerTiePolicy = "Nope";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task ShouldCreateLeague_AndPublishCreatedEvent_OnHappyPath()
    {
        // Pins the new MaxUsers = int.MaxValue behavior the base introduced
        // (none of the per-sport handlers set it before PR-E) plus the rest
        // of the shared body — persistence, member adds, event publish, and
        // Success result.
        const int SeasonYear = 2025;
        var currentUserId = Guid.NewGuid();
        var conferenceId = Guid.NewGuid();

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        request.Description = "  trimmable  ";
        request.DropLowWeeksCount = 2;
        request.ConferenceSlugs = ["sec"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.ConferenceSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [conferenceId] = "sec" });

        // Seed a synthetic user so the synthetic-member branch is exercised.
        var synthetic = new UserEntity
        {
            Username = "test_user_2",
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

        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeTrue();
        var leagueId = ((Success<Guid>)result).Value;

        var saved = await DataContext.PickemGroups
            .Include(g => g.Conferences)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == leagueId);

        saved.Should().NotBeNull();
        saved!.Sport.Should().Be(Sport.FootballNcaa);
        saved.League.Should().Be(League.NCAAF);
        saved.Name.Should().Be("My League");
        saved.Description.Should().Be("trimmable");
        saved.IsPublic.Should().BeTrue();
        saved.MaxUsers.Should().Be(int.MaxValue);
        saved.DropLowWeeksCount.Should().Be(2);
        saved.RankingFilter.Should().BeNull();
        saved.CommissionerUserId.Should().Be(currentUserId);
        saved.CreatedBy.Should().Be(currentUserId);
        saved.SeasonYear.Should().Be(SeasonYear);

        saved.Conferences.Should().ContainSingle()
            .Which.Should().Match<PickemGroupConference>(c =>
                c.ConferenceId == conferenceId && c.ConferenceSlug == "sec");

        saved.Members.Should().HaveCount(2);
        saved.Members.Should().Contain(m =>
            m.UserId == currentUserId && m.Role == LeagueRole.Commissioner);
        saved.Members.Should().Contain(m =>
            m.UserId == synthetic.Id && m.Role == LeagueRole.Member);

        eventBusMock.Verify(
            x => x.Publish(
                It.Is<PickemGroupCreated>(e => e.GroupId == leagueId && e.Sport == Sport.FootballNcaa),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldParseAndPersistRankingFilter_WhenSupplied()
    {
        // Pins the NCAA-only ApplySportSpecific hook: a non-null
        // RankingFilter string must round-trip to the parsed enum on the
        // saved entity.
        const int SeasonYear = 2025;
        var conferenceId = Guid.NewGuid();

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        request.RankingFilter = "AP_TOP_25";
        request.ConferenceSlugs = ["sec"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.ConferenceSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [conferenceId] = "sec" });

        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        result.IsSuccess.Should().BeTrue();
        var leagueId = ((Success<Guid>)result).Value;

        var saved = await DataContext.PickemGroups.FirstAsync(g => g.Id == leagueId);
        saved.RankingFilter.Should().Be(TeamRankingFilter.AP_TOP_25);
    }

    [Fact]
    public async Task ShouldFail_WhenAnyConferenceSlugNotResolved()
    {
        var request = BuildValidRequest();
        var currentUserId = Guid.NewGuid();
        var currentYear = DateTime.UtcNow.Year;

        var slugToGuid = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "acc",
            [Guid.NewGuid()] = "big12"
        };

        request.RankingFilter = "AP_TOP_25";
        request.ConferenceSlugs = ["acc", "big12", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(currentYear, request.ConferenceSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateFootballNcaaLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeFalse();
    }
}
