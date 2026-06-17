using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests;

[Collection("Sequential")]
public class ContestEnrichmentJobTests : ProducerTestBase<ContestEnrichmentJob<FootballDataContext>>
{
    private static readonly DateTime FixedNow =
        new(2026, 6, 16, 14, 0, 0, DateTimeKind.Utc);

    public ContestEnrichmentJobTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedNow);
    }

    [Fact(Skip = "Remove after testing")]
    public async Task Execute_EnqueuesOnePerCandidate_WhenContestIsPastStartAndUnfinalizedAndUncancelled()
    {
        // Arrange — three legitimate candidates spanning different StartDateUtc
        // values to exercise the ordering. None finalized, none cancelled.
        var ids = new[]
        {
            await SeedContestAsync(startDaysFromNow: -30),
            await SeedContestAsync(startDaysFromNow: -5),
            await SeedContestAsync(startDaysFromNow: -1)
        };

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()))
            .Callback<Expression<Func<IEnrichContests, Task>>>(expr =>
                enqueued.Add(EnrichContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentJob<FootballDataContext>>();

        // Act
        await sut.ExecuteAsync();

        // Assert — strict-equal so the OrderBy(StartDateUtc) contract is
        // actually verified. Seed order matches ascending StartDateUtc
        // (-30, -5, -1 days) so ids[] is already in the expected order.
        enqueued.Should().Equal(ids);
    }

    [Fact(Skip = "Remove after testing")]
    public async Task Execute_SkipsContestsThatAreAlreadyFinalized()
    {
        var ineligibleId = await SeedContestAsync(
            startDaysFromNow: -10,
            finalizedUtc: FixedNow.AddDays(-9));
        var eligibleId = await SeedContestAsync(startDaysFromNow: -5);

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()))
            .Callback<Expression<Func<IEnrichContests, Task>>>(expr =>
                enqueued.Add(EnrichContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        enqueued.Should().ContainSingle().Which.Should().Be(eligibleId);
        enqueued.Should().NotContain(ineligibleId);
    }

    [Fact(Skip = "Remove after testing")]
    public async Task Execute_SkipsContestsThatHaveCancelledUtcStamped()
    {
        // Regression — the cancelled-game exclusion is the whole reason the
        // CancelledUtc column exists. Without this filter, the unbounded query
        // would re-enqueue cancelled contests on every run.
        var cancelledId = await SeedContestAsync(
            startDaysFromNow: -10,
            cancelledUtc: FixedNow.AddDays(-9));
        var eligibleId = await SeedContestAsync(startDaysFromNow: -5);

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()))
            .Callback<Expression<Func<IEnrichContests, Task>>>(expr =>
                enqueued.Add(EnrichContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        enqueued.Should().ContainSingle().Which.Should().Be(eligibleId);
        enqueued.Should().NotContain(cancelledId);
    }

    [Fact(Skip = "Remove after testing")]
    public async Task Execute_SkipsContestsWithFutureStartTime()
    {
        var futureId = await SeedContestAsync(startDaysFromNow: 2);
        var pastId = await SeedContestAsync(startDaysFromNow: -2);

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()))
            .Callback<Expression<Func<IEnrichContests, Task>>>(expr =>
                enqueued.Add(EnrichContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        enqueued.Should().ContainSingle().Which.Should().Be(pastId);
        enqueued.Should().NotContain(futureId);
    }

    [Fact]
    public async Task Execute_NoCandidates_ReturnsCleanlyWithoutEnqueuing()
    {
        // Seed only ineligible contests so the candidate query returns empty.
        await SeedContestAsync(startDaysFromNow: -10, finalizedUtc: FixedNow.AddDays(-9));
        await SeedContestAsync(startDaysFromNow: -10, cancelledUtc: FixedNow.AddDays(-9));
        await SeedContestAsync(startDaysFromNow: 5);

        var sut = Mocker.CreateInstance<ContestEnrichmentJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IEnrichContests>(It.IsAny<Expression<Func<IEnrichContests, Task>>>()),
            Times.Never);
    }

    private async Task<Guid> SeedContestAsync(
        int startDaysFromNow,
        DateTime? finalizedUtc = null,
        DateTime? cancelledUtc = null)
    {
        var contest = new FootballContest
        {
            Id = Guid.NewGuid(),
            Name = $"Contest {Guid.NewGuid():N}",
            ShortName = "TC",
            Sport = Sport.FootballNcaa,
            SeasonYear = 2025,
            StartDateUtc = FixedNow.AddDays(startDaysFromNow),
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            FinalizedUtc = finalizedUtc,
            CancelledUtc = cancelledUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.SaveChangesAsync();
        return contest.Id;
    }

    private static Guid? EnrichContestIdFromExpression(
        Expression<Func<IEnrichContests, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IEnrichContests.Process)) return null;
        if (call.Arguments.Count != 1) return null;

        var cmd = Expression.Lambda<Func<EnrichContestCommand>>(call.Arguments[0]).Compile()();
        return cmd.ContestId;
    }
}
