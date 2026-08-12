using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.FranchiseSeasons.Commands.RequestFranchiseSeasonSourcing;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.FranchiseSeasons;

/// <summary>
/// Bulk sourcing fan-out: one DocumentRequested (TeamSeason, full cascade)
/// per franchise season with a usable ESPN ref; the rest are counted and
/// skipped rather than failing the batch.
/// </summary>
public class RequestFranchiseSeasonSourcingCommandHandlerTests
    : ProducerTestBase<RequestFranchiseSeasonSourcingCommandHandler>
{
    private const int SeasonYear = 2026;

    // Fixed clock: deterministic seed data, per the no-DateTime.UtcNow rule.
    private static readonly DateTime FixedNow = new(2026, 7, 31, 12, 0, 0, DateTimeKind.Utc);

#nullable enable
    private async Task<FranchiseSeason> SeedFranchiseSeasonAsync(string? sourceUrl)
    {
        var franchiseId = Guid.NewGuid();
        var fsId = Guid.NewGuid();

        await FootballDataContext.Franchises.AddAsync(new Franchise
        {
            Id = franchiseId,
            Sport = Sport.FootballNfl,
            Name = $"Team {franchiseId:N}"[..12],
            Nickname = "Testers",
            Location = "Testville",
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            Slug = $"team-{franchiseId:N}"[..20],
            ColorCodeHex = "000000",
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        });

        var fs = new FranchiseSeason
        {
            Id = fsId,
            FranchiseId = franchiseId,
            SeasonYear = SeasonYear,
            Slug = $"fs-{fsId:N}"[..20],
            Location = "Testville",
            Name = "Test Team",
            Abbreviation = "TST",
            DisplayName = "Test Team",
            DisplayNameShort = "Test",
            ColorCodeHex = "000000",
            IsActive = true,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        if (sourceUrl is not null)
        {
            fs.ExternalIds.Add(new FranchiseSeasonExternalId
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = fsId,
                Provider = SourceDataProvider.Espn,
                Value = fsId.ToString(),
                SourceUrlHash = fsId.ToString("N"),
                SourceUrl = sourceUrl,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }

        await FootballDataContext.FranchiseSeasons.AddAsync(fs);
        await FootballDataContext.SaveChangesAsync();
        return fs;
    }

    private RequestFranchiseSeasonSourcingCommandHandler CreateHandler()
    {
        // Real validator with a fixed clock — bounds behavior is part of the
        // contract, not something to mock away.
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<FluentValidation.IValidator<RequestFranchiseSeasonSourcingCommand>>(
            new RequestFranchiseSeasonSourcingCommandValidator(Mocker.Get<IDateTimeProvider>()));
        Mocker.GetMock<IGenerateExternalRefIdentities>()
            .Setup(x => x.Generate(It.IsAny<Uri>()))
            .Returns((Uri u) => new ExternalRefIdentity(
                Guid.NewGuid(),
                u.ToString().GetHashCode().ToString("X"),
                u.ToString()));
        // Direct delivery is required (read-only handler; the bus-outbox would
        // never flush). Return a real disposable so `using` is safe.
        Mocker.GetMock<IMessageDeliveryScope>()
            .Setup(x => x.Use(It.IsAny<DeliveryMode>()))
            .Returns(new NoopDisposable());
        return Mocker.CreateInstance<RequestFranchiseSeasonSourcingCommandHandler>();
    }

    [Fact]
    public async Task PublishesOneRequestPerFranchiseSeason_FullCascade()
    {
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/1");
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/2");

        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        result.IsSuccess.Should().BeTrue();
        var published = new List<DocumentRequested>();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    CaptureAndMatch(published, e) &&
                    e.DocumentType == DocumentType.TeamSeason &&
                    e.Sport == Sport.FootballNfl &&
                    e.SeasonYear == SeasonYear &&
                    // Full cascade: no linked-type filter (it would propagate
                    // down the tree and strangle the schedule's children).
                    e.IncludeLinkedDocumentTypes == null),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // One batch, one correlation id — the stated Seq handle for the run.
        published.Select(e => e.CorrelationId).Distinct().Should().ContainSingle();

        // Direct delivery, not the bus-outbox — this handler saves nothing, so
        // the outbox would silently swallow the events (the prod bug that let
        // Producer log 202 while Provider received nothing).
        Mocker.GetMock<IMessageDeliveryScope>()
            .Verify(x => x.Use(DeliveryMode.Direct), Times.Once);
    }

    [Fact]
    public async Task NarrowedRequest_ThreadsFilterIntoEveryDocumentRequested()
    {
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/1");
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/2");

        // The records backfill case: narrow the TeamSeason cascade to
        // FranchiseSeasonRecord child documents only.
        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(
                SeasonYear,
                Sport.FootballNfl,
                [DocumentType.TeamSeasonRecord]));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.DocumentType == DocumentType.TeamSeason &&
                    e.IncludeLinkedDocumentTypes != null &&
                    e.IncludeLinkedDocumentTypes.Count == 1 &&
                    e.IncludeLinkedDocumentTypes.Contains(DocumentType.TeamSeasonRecord)),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    private sealed class NoopDisposable : IDisposable
    {
        public void Dispose() { }
    }

    private static bool CaptureAndMatch(List<DocumentRequested> sink, DocumentRequested e)
    {
        sink.Add(e);
        return true;
    }

    [Fact]
    public async Task SeasonYear2000_IsTheInclusiveFloor_PassesValidation()
    {
        // 2000 is a SOURCED season (the historical floor). NotFound (no
        // seeded franchise seasons) proves the command got past validation.
        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(2000, Sport.FootballNfl));

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task ImplausibleSeasonYear_FailsValidation_PublishesNothing()
    {
        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(1999, Sport.FootballNfl));

        result.Status.Should().Be(ResultStatus.Validation);
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishFailure_ForOneSeason_DoesNotAbandonTheBatch()
    {
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/1");
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/2");

        // First publish throws (broker hiccup), the second succeeds.
        Mocker.GetMock<IEventBus>()
            .SetupSequence(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("broker hiccup"))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        // The batch completes (correlation id preserved) and BOTH seasons were
        // attempted -- a single failure must not strand the rest.
        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SkipsFranchiseSeasonsWithoutUsableRef_ContinuesBatch()
    {
        await SeedFranchiseSeasonAsync("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2026/teams/1");
        await SeedFranchiseSeasonAsync(sourceUrl: null);
        await SeedFranchiseSeasonAsync(sourceUrl: "not-a-uri");

        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoFranchiseSeasons_ReturnsNotFound_PublishesNothing()
    {
        var result = await CreateHandler().ExecuteAsync(
            new RequestFranchiseSeasonSourcingCommand(SeasonYear, Sport.FootballNfl));

        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
