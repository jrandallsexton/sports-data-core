#nullable enable

using AutoFixture;

using FluentAssertions;

using FluentValidation;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.FranchiseSeasons.Commands.RequestSingleFranchiseSeasonSourcing;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons.Commands;

public class RequestSingleFranchiseSeasonSourcingCommandHandlerTests
    : ProducerTestBase<RequestSingleFranchiseSeasonSourcingCommandHandler>
{
    private const string EspnUrl =
        "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/teams/6";

    private readonly Guid _correlationId = Guid.NewGuid();

    public RequestSingleFranchiseSeasonSourcingCommandHandlerTests()
    {
        SetSport(Sport.FootballNcaa);
        Mocker.Use<IValidator<RequestSingleFranchiseSeasonSourcingCommand>>(
            new RequestSingleFranchiseSeasonSourcingCommandValidator());
        Mocker.Use<IGenerateExternalRefIdentities>(new ExternalRefIdentityGenerator());
    }

    private void SetSport(Sport sport) =>
        Mocker.GetMock<IAppMode>().SetupGet(x => x.CurrentSport).Returns(sport);

    private RequestSingleFranchiseSeasonSourcingCommand Command(Guid franchiseSeasonId) =>
        new(franchiseSeasonId, _correlationId);

    private void VerifyNothingPublished() =>
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()), Times.Never);

    private async Task<Guid> SeedFranchiseSeasonAsync(int seasonYear, Sport sport = Sport.FootballNcaa, string? espnUrl = null)
    {
        var franchise = Fixture.Build<Franchise>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, "Test Franchise")
            .With(x => x.DisplayName, "Test Franchise")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, $"test-franchise-{Guid.NewGuid()}")
            .With(x => x.ColorCodeHex, "#000000")
            .With(x => x.Sport, sport)
            .Create();

        var franchiseSeason = Fixture.Build<FranchiseSeason>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.FranchiseId, franchise.Id)
            .With(x => x.SeasonYear, seasonYear)
            .With(x => x.Name, "Test Franchise Season")
            .With(x => x.DisplayName, "Test Franchise Season")
            .With(x => x.DisplayNameShort, "Test")
            .With(x => x.Abbreviation, "TEST")
            .With(x => x.Location, "Test City")
            .With(x => x.Slug, $"test-franchise-season-{Guid.NewGuid()}")
            .With(x => x.ColorCodeHex, "#000000")
            .Create();

        await FootballDataContext.Franchises.AddAsync(franchise);
        await FootballDataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        if (espnUrl is not null)
        {
            await FootballDataContext.Set<FranchiseSeasonExternalId>().AddAsync(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeason.Id,
                Provider = SourceDataProvider.Espn,
                Value = "6",
                SourceUrl = espnUrl,
                // A deliberately unparseable ref still needs a stored hash.
                SourceUrlHash = Uri.TryCreate(espnUrl, UriKind.Absolute, out var parsed)
                    ? HashProvider.GenerateHashFromUri(parsed)
                    : "unparseable"
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return franchiseSeason.Id;
    }

    [Fact]
    public async Task Execute_PublishesOneTeamSeasonRequest_WithTheFullCascade_Direct_UnderTheCallersCorrelationId()
    {
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: EspnUrl);
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeTrue();
        result.Status.Should().Be(ResultStatus.Accepted);
        // The caller's id is echoed back, never a fresh one: API and
        // Producer must log under the same Seq handle.
        result.Value.Should().Be(_correlationId);

        var expectedHash = HashProvider.GenerateHashFromUri(new Uri(EspnUrl));
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.Is<IEnumerable<DocumentRequested>>(batch =>
                batch.Count() == 1 &&
                batch.All(d =>
                    d.Id == expectedHash &&
                    d.ParentId == id.ToString() &&
                    d.DocumentType == DocumentType.TeamSeason &&
                    d.SourceDataProvider == SourceDataProvider.Espn &&
                    d.Sport == Sport.FootballNcaa &&
                    d.SeasonYear == 2026 &&
                    d.CorrelationId == _correlationId &&
                    d.Uri.ToString().Contains("/teams/6") &&
                    // Null = spawn every child type. Narrowing would stop the
                    // events' own children from being sourced.
                    d.IncludeLinkedDocumentTypes == null)),
            It.IsAny<CancellationToken>()), Times.Once);
        Mocker.GetMock<IMessageDeliveryScope>().Verify(x => x.Use(DeliveryMode.Direct), Times.Once);
    }

    [Fact]
    public async Task Execute_Baseball_PublishesUnderTheCurrentSport()
    {
        SetSport(Sport.BaseballMlb);
        var id = await SeedFranchiseSeasonAsync(2026, Sport.BaseballMlb,
            espnUrl: "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/seasons/2026/teams/10");
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(x => x.PublishBatch(
            It.Is<IEnumerable<DocumentRequested>>(b => b.Count() == 1 && b.All(d => d.Sport == Sport.BaseballMlb)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Execute_NoEspnRef_ReturnsValidationFailure_AndPublishesNothing()
    {
        // Unlike the bulk handler's skip, sourcing is this action's only job:
        // a season with nothing to source from must read as a failure.
        var id = await SeedFranchiseSeasonAsync(2026);
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        ((Failure<Guid>)result).Errors.Single().ErrorMessage.Should().Contain("no usable ESPN TeamSeason ref");
        VerifyNothingPublished();
    }

    [Fact]
    public async Task Execute_UnparseableEspnRef_ReturnsValidationFailure_LikeNoRef()
    {
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: "not a url");
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        VerifyNothingPublished();
    }

    [Fact]
    public async Task Execute_UnknownFranchiseSeason_ReturnsNotFound_AndPublishesNothing()
    {
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();

        var result = await sut.ExecuteAsync(Command(Guid.NewGuid()));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        VerifyNothingPublished();
    }

    [Theory]
    [InlineData(true, false)]  // empty franchise season id
    [InlineData(false, true)]  // empty correlation id
    public async Task Execute_InvalidCommand_ReturnsValidationFailure_AndPublishesNothing(bool emptyId, bool emptyCorrelation)
    {
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();
        var command = new RequestSingleFranchiseSeasonSourcingCommand(
            emptyId ? Guid.Empty : Guid.NewGuid(),
            emptyCorrelation ? Guid.Empty : Guid.NewGuid());

        var result = await sut.ExecuteAsync(command);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Validation);
        VerifyNothingPublished();
    }

    [Fact]
    public async Task Execute_PublishThrows_ReportsFailure_NamingTheCorrelationId_NotTheException()
    {
        // A synchronous admin request: the operator sees the failure and the
        // Seq handle, never the raw exception text (the UI renders the message).
        var id = await SeedFranchiseSeasonAsync(2026, espnUrl: EspnUrl);
        Mocker.GetMock<IEventBus>()
            .Setup(x => x.PublishBatch(It.IsAny<IEnumerable<DocumentRequested>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker down: amqp://secret@host"));
        var sut = Mocker.CreateInstance<RequestSingleFranchiseSeasonSourcingCommandHandler>();

        var result = await sut.ExecuteAsync(Command(id));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Error);
        var message = ((Failure<Guid>)result).Errors.Single().ErrorMessage;
        message.Should().Contain(_correlationId.ToString());
        message.Should().NotContain("amqp://");
    }
}
