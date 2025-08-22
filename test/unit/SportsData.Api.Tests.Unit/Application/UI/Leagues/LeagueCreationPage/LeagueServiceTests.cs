using FluentAssertions;

using Moq;

using SportsData.Api.Application.UI.Leagues;
using SportsData.Api.Application.UI.Leagues.LeagueCreationPage.Dtos;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues.LeagueCreationPage;

public class LeagueServiceTests : ApiTestBase<LeagueService>
{
    private CreateLeagueRequest BuildValidRequest() => new()
    {
        Name = "My League",
        PickType = "StraightUp",
        TiebreakerType = "TotalPoints",
        TiebreakerTiePolicy = "EarliestSubmission",
        ConferenceSlugs = new List<string> { "sec" },
        UseConfidencePoints = false,
        IsPublic = true
    };

    //[Fact]
    //public async Task WhenRequestIsValid_ShouldCreateLeague()
    //{
    //    // Arrange
    //    var request = Fixture.Build<CreateLeagueRequest>()
    //        .With(x => x.Name, "My League")
    //        .With(x => x.PickType, PickType.StraightUp.ToString())
    //        .With(x => x.TiebreakerType, TiebreakerType.TotalPoints.ToString())
    //        .With(x => x.TiebreakerTiePolicy, TiebreakerTiePolicy.EarliestSubmission.ToString())
    //        .With(x => x.ConferenceSlugs, new List<string> { "sec", "bigten" })
    //        .Create();

    //    var franchiseIdMap = new Dictionary<string, Guid>
    //    {
    //        { "sec", Guid.NewGuid() },
    //        { "bigten", Guid.NewGuid() }
    //    };

    //    var expectedLeagueId = Guid.NewGuid();
    //    var userId = Guid.NewGuid();

    //    Mocker.GetMock<IProvideCanonicalData>()
    //        .Setup(x => x.GetFranchiseIdsBySlugsAsync(Sport.FootballNcaa, request.ConferenceSlugs))
    //        .ReturnsAsync(franchiseIdMap);

    //    Mocker.GetMock<ICreateLeagueCommandHandler>()
    //        .Setup(x => x.ExecuteAsync(It.IsAny<CreateLeagueCommand>(), It.IsAny<CancellationToken>()))
    //        .ReturnsAsync(expectedLeagueId);

    //    var sut = Mocker.CreateInstance<LeagueCreationService>();

    //    // Act
    //    var result = await sut.CreateAsync(request, userId);

    //    // Assert
    //    result.Should().Be(expectedLeagueId);

    //    Mocker.GetMock<ICreateLeagueCommandHandler>().Verify(x =>
    //        x.ExecuteAsync(It.Is<CreateLeagueCommand>(cmd =>
    //            cmd.Name == request.Name &&
    //            cmd.PickType == PickType.StraightUp &&
    //            cmd.TiebreakerType == TiebreakerType.TotalPoints &&
    //            cmd.TiebreakerTiePolicy == TiebreakerTiePolicy.EarliestSubmission &&
    //            cmd.ConferenceIds.SequenceEqual(franchiseIdMap.Values) &&
    //            cmd.CommissionerUserId == userId &&
    //            cmd.CreatedBy == userId
    //        ), It.IsAny<CancellationToken>()), Times.Once);
    //}

    [Fact]
    public async Task ShouldThrow_WhenNameIsNull()
    {
        var sut = Mocker.CreateInstance<LeagueService>();
        var request = BuildValidRequest();
        request.Name = null!;

        var act = () => sut.CreateAsync(request, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("League name is required.*");
    }

    [Fact]
    public async Task ShouldThrow_WhenPickTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<LeagueService>();
        var request = BuildValidRequest();
        request.PickType = "Garbage";

        var act = () => sut.CreateAsync(request, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid pick type: Garbage");
    }

    [Fact]
    public async Task ShouldThrow_WhenTiebreakerTypeIsInvalid()
    {
        var sut = Mocker.CreateInstance<LeagueService>();
        var request = BuildValidRequest();
        request.TiebreakerType = "Garbage";

        var act = () => sut.CreateAsync(request, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid tiebreaker type: Garbage");
    }

    [Fact]
    public async Task ShouldThrow_WhenTiebreakerTiePolicyIsInvalid()
    {
        var sut = Mocker.CreateInstance<LeagueService>();
        var request = BuildValidRequest();
        request.TiebreakerTiePolicy = "Nope";

        var act = () => sut.CreateAsync(request, Guid.NewGuid());

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Invalid tiebreaker tie policy: Nope");
    }

    [Fact]
    public async Task ShouldThrow_WhenAnyConferenceSlugNotResolved()
    {
        var request = BuildValidRequest();
        var currentUserId = Guid.NewGuid();

        var slugToGuid = new Dictionary<string, Guid>
        {
            ["acc"] = Guid.NewGuid(),
            ["big12"] = Guid.NewGuid(),
        };

        request.ConferenceSlugs = new List<string> { "acc", "big12", "garbage" };

        Mocker.GetMock<IProvideCanonicalData>()
            .Setup(x => x.GetFranchiseIdsBySlugsAsync(Sport.FootballNcaa, request.ConferenceSlugs))
            .ReturnsAsync(slugToGuid);

        var sut = Mocker.CreateInstance<LeagueService>();

        var act = () => sut.CreateAsync(request, currentUserId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Unknown conference slugs: garbage");
    }
}
