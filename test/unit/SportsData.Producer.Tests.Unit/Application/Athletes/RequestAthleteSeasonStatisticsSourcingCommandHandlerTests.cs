using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Producer.Application.Athletes.Commands.RequestAthleteSeasonStatisticsSourcing;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Athletes;

/// <summary>
/// Bulk statistics backfill fan-out: one DocumentRequested per ACTIVE
/// athlete season with a usable ESPN ref, targeting a SYNTHESIZED
/// season-type-scoped statistics URL (never the athlete document's own
/// statistics ref — ESPN points that at the prior season until the new one
/// has data, which is the mislabeling this backfill repairs).
/// </summary>
public class RequestAthleteSeasonStatisticsSourcingCommandHandlerTests
    : ProducerTestBase<RequestAthleteSeasonStatisticsSourcingCommandHandler>
{
    private const int SeasonYear = 2025;

    // Fixed clock: deterministic seed data, per the no-DateTime.UtcNow rule.
    private static readonly DateTime FixedNow = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

#nullable enable
    private async Task<AthleteSeason> SeedAthleteSeasonAsync(
        string? sourceUrl,
        bool isActive = true,
        int seasonYear = SeasonYear)
    {
        var fsId = Guid.NewGuid();
        await FootballDataContext.FranchiseSeasons.AddAsync(new FranchiseSeason
        {
            Id = fsId,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = seasonYear,
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
        });

        var athleteSeasonId = Guid.NewGuid();
        var athleteSeason = new FootballAthleteSeason
        {
            Id = athleteSeasonId,
            AthleteId = Guid.NewGuid(),
            FranchiseSeasonId = fsId,
            PositionId = Guid.NewGuid(),
            FirstName = "Test",
            LastName = "Athlete",
            IsActive = isActive,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        if (sourceUrl is not null)
        {
            athleteSeason.ExternalIds.Add(new AthleteSeasonExternalId
            {
                Id = Guid.NewGuid(),
                AthleteSeasonId = athleteSeasonId,
                Provider = SourceDataProvider.Espn,
                Value = athleteSeasonId.ToString(),
                SourceUrlHash = athleteSeasonId.ToString("N"),
                SourceUrl = sourceUrl,
                CreatedUtc = FixedNow,
                CreatedBy = Guid.NewGuid()
            });
        }

        await FootballDataContext.AthleteSeasons.AddAsync(athleteSeason);
        await FootballDataContext.SaveChangesAsync();
        return athleteSeason;
    }

    private RequestAthleteSeasonStatisticsSourcingCommandHandler CreateHandler()
    {
        // Real validator with a fixed clock — bounds behavior is part of the
        // contract, not something to mock away.
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(FixedNow);
        Mocker.Use<FluentValidation.IValidator<RequestAthleteSeasonStatisticsSourcingCommand>>(
            new RequestAthleteSeasonStatisticsSourcingCommandValidator(Mocker.Get<IDateTimeProvider>()));
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
        return Mocker.CreateInstance<RequestAthleteSeasonStatisticsSourcingCommandHandler>();
    }

    [Fact]
    public async Task PublishesOneRequestPerActiveAthleteSeason_WithSynthesizedTypeScopedUrl()
    {
        var seeded = await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/4870906");
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/5078810");

        var result = await CreateHandler().ExecuteAsync(
            new RequestAthleteSeasonStatisticsSourcingCommand(SeasonYear, Sport.FootballNcaa));

        result.IsSuccess.Should().BeTrue();
        var published = new List<DocumentRequested>();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    CaptureAndMatch(published, e) &&
                    e.DocumentType == DocumentType.AthleteSeasonStatistics &&
                    e.Sport == Sport.FootballNcaa &&
                    e.SeasonYear == SeasonYear),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));

        // The URL is SYNTHESIZED with the explicit /types/3/ scope — never
        // taken from the athlete document's (prior-season) statistics ref.
        published.Select(e => e.Uri.ToString()).Should().BeEquivalentTo(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/athletes/4870906/statistics",
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/athletes/5078810/statistics");

        // ParentId carries the spawning roster row; the processor's season
        // guard re-verifies it against the ref's season regardless.
        published.Select(e => e.ParentId).Should().Contain(seeded.Id.ToString());

        // One batch, one correlation id — the Seq handle for the run.
        published.Select(e => e.CorrelationId).Distinct().Should().ContainSingle();

        // Direct delivery, not the bus-outbox — this handler saves nothing,
        // so the outbox would silently swallow the events.
        Mocker.GetMock<IMessageDeliveryScope>()
            .Verify(x => x.Use(DeliveryMode.Direct), Times.Once);
    }

    [Fact]
    public async Task InactiveOtherSeasonAndReflessRows_AreExcludedOrSkipped()
    {
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/1");
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/2",
            isActive: false);
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024/athletes/3",
            seasonYear: 2024);
        await SeedAthleteSeasonAsync(sourceUrl: null); // no ESPN ref: logged skip

        var result = await CreateHandler().ExecuteAsync(
            new RequestAthleteSeasonStatisticsSourcingCommand(SeasonYear, Sport.FootballNcaa));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e =>
                    e.Uri.ToString().Contains("/athletes/1/")),
                It.IsAny<CancellationToken>()),
            Times.Once);
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SeasonType2_ScopesTheSynthesizedUrlToRegularSeason()
    {
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/4870906");

        var result = await CreateHandler().ExecuteAsync(
            new RequestAthleteSeasonStatisticsSourcingCommand(SeasonYear, Sport.FootballNcaa, SeasonType: 2));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e => e.Uri.ToString().Contains("/types/2/")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task InvalidSeasonType_FailsValidation_PublishesNothing()
    {
        var result = await CreateHandler().ExecuteAsync(
            new RequestAthleteSeasonStatisticsSourcingCommand(SeasonYear, Sport.FootballNcaa, SeasonType: 1));

        result.Status.Should().Be(ResultStatus.Validation);
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SuppliedCorrelationId_IsUsedOnEveryPublishedEvent()
    {
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/1");
        await SeedAthleteSeasonAsync(
            "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/athletes/2");

        // The controller mints the id, returns it in the 202, and enqueues
        // the handler — the operator's Seq handle must match the response.
        var suppliedId = Guid.NewGuid();

        var result = await CreateHandler().ExecuteAsync(
            new RequestAthleteSeasonStatisticsSourcingCommand(
                SeasonYear, Sport.FootballNcaa, 3, suppliedId));

        result.IsSuccess.Should().BeTrue();
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(
                It.Is<DocumentRequested>(e => e.CorrelationId == suppliedId),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task NoActiveAthleteSeasons_ReturnsNotFound_PublishesNothing()
    {
        var result = await CreateHandler().ExecuteAsync(
            new RequestAthleteSeasonStatisticsSourcingCommand(SeasonYear, Sport.FootballNcaa));

        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<IEventBus>().Verify(
            x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
}
