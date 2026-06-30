using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague;
using SportsData.Api.Application.UI.Leagues.Commands.CreateFootballNflLeague.Dtos;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Core.Infrastructure.Clients.Franchise;

using Xunit;

using FailureResult = SportsData.Core.Common.Failure<System.Guid>;
// Alias: SportsData.Api.Tests.Unit.Application.User is a sibling namespace
// that beats the User entity type in name resolution from this file.
using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.Commands.CreateFootballNflLeague;

public class CreateFootballNflLeagueCommandHandlerTests : ApiTestBase<CreateFootballNflLeagueCommandHandler>
{
    private readonly Mock<IFranchiseClientFactory> _franchiseClientFactoryMock;
    private readonly Mock<IProvideFranchises> _franchiseClientMock;

    public CreateFootballNflLeagueCommandHandlerTests()
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
        Mocker.Use<FluentValidation.IValidator<CreateFootballNflLeagueRequest>>(
            new CreateFootballNflLeagueRequestValidator(dateTimeProviderMock.Object));
    }

    private CreateFootballNflLeagueRequest BuildValidRequest() => new()
    {
        Name = "My NFL League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        DivisionSlugs = new List<string> { "afc-east" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    [Fact]
    public async Task ShouldFail_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.Name = null!;

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        var failure = result.Should().BeOfType<FailureResult>().Subject;
        failure.Status.Should().Be(ResultStatus.Validation);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(CreateFootballNflLeagueRequest.Name));
        failure.Errors.Single().ErrorMessage.Should().Contain("League name is required");
    }

    [Fact]
    public async Task ShouldFail_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var result = await sut.ExecuteAsync(request, Guid.NewGuid());

        var failure = result.Should().BeOfType<FailureResult>().Subject;
        failure.Status.Should().Be(ResultStatus.Validation);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(CreateFootballNflLeagueRequest.PickType));
        failure.Errors.Single().ErrorMessage.Should().Contain("Invalid pick type: Garbage");
    }

    [Fact]
    public async Task ShouldCreateLeague_AndPublishCreatedEvent_OnHappyPath()
    {
        // Mirrors the NCAA happy-path test against the NFL handler's
        // SportMode/LeagueMode and division-slug input. Pins the new
        // MaxUsers = int.MaxValue behavior the base introduced.
        const int SeasonYear = 2025;
        var currentUserId = Guid.NewGuid();
        var divisionId = Guid.NewGuid();

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        request.DivisionSlugs = ["afc-east"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<Guid, string> { [divisionId] = "afc-east" });

        var synthetic = new UserEntity
        {
            Username = "test_user_3",
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

        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        result.IsSuccess.Should().BeTrue();
        var leagueId = ((Success<Guid>)result).Value;

        var saved = await DataContext.PickemGroups
            .Include(g => g.Conferences)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == leagueId);

        saved.Should().NotBeNull();
        saved!.Sport.Should().Be(Sport.FootballNfl);
        saved.League.Should().Be(League.NFL);
        saved.Name.Should().Be("My NFL League");
        saved.MaxUsers.Should().Be(int.MaxValue);
        saved.CommissionerUserId.Should().Be(currentUserId);

        saved.Conferences.Should().ContainSingle()
            .Which.Should().Match<PickemGroupConference>(c =>
                c.ConferenceId == divisionId && c.ConferenceSlug == "afc-east");

        saved.Members.Should().HaveCount(2);
        saved.Members.Should().Contain(m =>
            m.UserId == currentUserId && m.Role == LeagueRole.Commissioner);
        saved.Members.Should().Contain(m =>
            m.UserId == synthetic.Id && m.Role == LeagueRole.Member);

        eventBusMock.Verify(
            x => x.Publish(
                It.Is<PickemGroupCreated>(e => e.GroupId == leagueId && e.Sport == Sport.FootballNfl),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ShouldFail_WhenAnyDivisionSlugNotResolved()
    {
        // Pin SeasonYear on the request so the handler's DateTime.UtcNow.Year
        // fallback never fires — the mock setup below stays deterministic.
        const int SeasonYear = 2025;

        var request = BuildValidRequest();
        request.SeasonYear = SeasonYear;
        var currentUserId = Guid.NewGuid();

        var slugToGuid = new Dictionary<Guid, string>
        {
            [Guid.NewGuid()] = "afc-east",
            [Guid.NewGuid()] = "afc-north"
        };

        request.DivisionSlugs = ["afc-east", "afc-north", "garbage"];

        _franchiseClientMock
            .Setup(x => x.GetConferenceIdsBySlugs(SeasonYear, request.DivisionSlugs, It.IsAny<CancellationToken>()))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<CreateFootballNflLeagueCommandHandler>();

        var result = await sut.ExecuteAsync(request, currentUserId);

        var failure = result.Should().BeOfType<FailureResult>().Subject;
        failure.Status.Should().Be(ResultStatus.Validation);
        failure.Errors.Should().ContainSingle()
            .Which.PropertyName.Should().Be(nameof(CreateFootballNflLeagueRequest.DivisionSlugs));
        failure.Errors.Single().ErrorMessage.Should().Contain("Unknown division slugs")
            .And.Contain("garbage");
    }
}
