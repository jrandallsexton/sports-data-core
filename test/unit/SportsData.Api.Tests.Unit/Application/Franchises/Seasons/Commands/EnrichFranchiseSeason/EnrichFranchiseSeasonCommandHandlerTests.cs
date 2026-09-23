using FluentAssertions;

using FluentValidation;
using FluentValidation.Results;

using Moq;

using SportsData.Api.Application.Franchises.Seasons;
using SportsData.Api.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;
using SportsData.Api.Infrastructure.Refs;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Infrastructure.Clients.Franchise.Queries;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Franchises.Seasons.Commands.EnrichFranchiseSeason;

public class EnrichFranchiseSeasonCommandHandlerTests : UnitTestBase<EnrichFranchiseSeasonCommandHandler>
{
    private readonly Mock<IProvideFranchises> _client = new();
    private readonly Guid _franchiseId = Guid.NewGuid();
    private readonly Guid _franchiseSeasonId = Guid.NewGuid();

    private static readonly Uri SelfRef = new("https://api.test/api/football/ncaa/franchises/f/seasons/2026");
    private static readonly Uri FranchiseRef = new("https://api.test/api/football/ncaa/franchises/f");

    public EnrichFranchiseSeasonCommandHandlerTests()
    {
        Mocker.Use<IValidator<EnrichFranchiseSeasonCommand>>(new EnrichFranchiseSeasonCommandValidator());
        Mocker.GetMock<IFranchiseClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_client.Object);
        Mocker.GetMock<IGenerateApiResourceRefs>()
            .Setup(x => x.ForFranchiseSeason(_franchiseId, 2026, "football", "ncaa"))
            .Returns(SelfRef);
        Mocker.GetMock<IGenerateApiResourceRefs>()
            .Setup(x => x.ForFranchise(_franchiseId, "football", "ncaa"))
            .Returns(FranchiseRef);
    }

    private void FranchiseFound() =>
        _client.Setup(x => x.GetFranchiseById("sam-houston-bearkats", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<GetFranchiseByIdResponse>(
                new GetFranchiseByIdResponse(new FranchiseDto { Id = _franchiseId, Slug = "sam-houston-bearkats" })));

    private void SeasonFound() =>
        _client.Setup(x => x.GetFranchiseSeasonById(_franchiseId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<GetFranchiseSeasonByIdResponse>(
                new GetFranchiseSeasonByIdResponse(new FranchiseSeasonDto { Id = _franchiseSeasonId, FranchiseId = _franchiseId, SeasonYear = 2026 })));

    private static EnrichFranchiseSeasonCommand Command() =>
        new("football", "ncaa", "sam-houston-bearkats", 2026);

    [Fact]
    public async Task Execute_ResolvesSlugAndSeason_ThenAsksProducerToEnrichThatFranchiseSeason()
    {
        FranchiseFound();
        SeasonFound();
        var correlationId = Guid.NewGuid();
        _client.Setup(x => x.EnrichFranchiseSeason(_franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<Guid>(correlationId, ResultStatus.Accepted));

        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(Command());

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        result.Value.FranchiseId.Should().Be(_franchiseId);
        result.Value.FranchiseSeasonId.Should().Be(_franchiseSeasonId);
        result.Value.SeasonYear.Should().Be(2026);
        result.Value.CorrelationId.Should().Be(correlationId);

        // HATEOAS: ref + self on the franchise season this was requested for,
        // plus the franchise the reader would navigate up to.
        result.Value.Ref.Should().Be(SelfRef);
        result.Value.Links["self"].Should().Be(SelfRef);
        result.Value.Links["franchise"].Should().Be(FranchiseRef);

        // Resolved by sport mode from the route, never a hardcoded client.
        Mocker.GetMock<IFranchiseClientFactory>().Verify(x => x.Resolve(Sport.FootballNcaa), Times.Once);
    }

    [Fact]
    public async Task Execute_UnknownFranchise_ReturnsNotFound_WithoutCallingEnrich()
    {
        _client.Setup(x => x.GetFranchiseById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<GetFranchiseByIdResponse>(
                new GetFranchiseByIdResponse(null), ResultStatus.NotFound, [new ValidationFailure("id", "nope")]));

        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(Command());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        _client.Verify(x => x.EnrichFranchiseSeason(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_UnknownSeason_ReturnsNotFound_WithoutCallingEnrich()
    {
        FranchiseFound();
        _client.Setup(x => x.GetFranchiseSeasonById(_franchiseId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<GetFranchiseSeasonByIdResponse>(
                new GetFranchiseSeasonByIdResponse(null), ResultStatus.NotFound, [new ValidationFailure("season", "nope")]));

        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(Command());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        _client.Verify(x => x.EnrichFranchiseSeason(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_FranchiseLookupFailsForAnotherReason_PassesThatStatusThrough_NotNotFound()
    {
        // A Producer outage must not read as a data gap: the operator would
        // debug the wrong problem.
        _client.Setup(x => x.GetFranchiseById(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<GetFranchiseByIdResponse>(
                new GetFranchiseByIdResponse(null), ResultStatus.Error, [new ValidationFailure("Producer", "Producer returned 503")]));

        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(Command());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        ((Failure<EnrichFranchiseSeasonResponseDto>)result).Errors.Should().ContainSingle(e => e.ErrorMessage == "Producer returned 503");
        _client.Verify(x => x.EnrichFranchiseSeason(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_SeasonLookupFailsForAnotherReason_PassesThatStatusThrough_NotNotFound()
    {
        FranchiseFound();
        _client.Setup(x => x.GetFranchiseSeasonById(_franchiseId, 2026, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<GetFranchiseSeasonByIdResponse>(
                new GetFranchiseSeasonByIdResponse(null), ResultStatus.BadRequest, [new ValidationFailure("Producer", "bad request")]));

        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(Command());

        result.Status.Should().Be(ResultStatus.BadRequest);
        _client.Verify(x => x.EnrichFranchiseSeason(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_ProducerRefuses_PassesTheFailureThrough()
    {
        FranchiseFound();
        SeasonFound();
        _client.Setup(x => x.EnrichFranchiseSeason(_franchiseSeasonId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<Guid>(default, ResultStatus.Error, [new ValidationFailure("x", "Producer returned 500")]));

        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(Command());

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        ((Failure<EnrichFranchiseSeasonResponseDto>)result).Errors.Should().ContainSingle(e => e.ErrorMessage == "Producer returned 500");
    }

    [Fact]
    public async Task Execute_UnsupportedSportLeague_ReturnsBadRequest()
    {
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(new EnrichFranchiseSeasonCommand("curling", "wcf", "x", 2026));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.BadRequest);
        _client.Verify(x => x.GetFranchiseById(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("", "ncaa", "slug", 2026)]
    [InlineData("football", "", "slug", 2026)]
    [InlineData("football", "ncaa", "", 2026)]
    [InlineData("football", "ncaa", "slug", 1850)]
    [InlineData("football", "ncaa", "slug", 2226)]
    public async Task Execute_InvalidCommand_ReturnsValidation_BeforeAnyLookup(string sport, string league, string slug, int year)
    {
        var sut = Mocker.CreateInstance<EnrichFranchiseSeasonCommandHandler>();
        var result = await sut.ExecuteAsync(new EnrichFranchiseSeasonCommand(sport, league, slug, year));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        Mocker.GetMock<IFranchiseClientFactory>().Verify(x => x.Resolve(It.IsAny<Sport>()), Times.Never);
    }
}
