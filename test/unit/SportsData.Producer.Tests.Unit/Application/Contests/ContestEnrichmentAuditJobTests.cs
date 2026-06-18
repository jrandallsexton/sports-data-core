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
public class ContestEnrichmentAuditJobTests : ProducerTestBase<ContestEnrichmentAuditJob<FootballDataContext>>
{
    private static readonly DateTime FixedNow =
        new(2026, 6, 19, 6, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Execute_EnqueuesOnePerCandidate_OldestFinalizedFirst()
    {
        // Three legitimate candidates: FinalizedUtc set, AuditedUtc null.
        // Seed in non-chronological order to verify ordering is by FinalizedUtc asc.
        var middleId = await SeedContestAsync(finalizedUtc: FixedNow.AddDays(-5));
        var oldestId = await SeedContestAsync(finalizedUtc: FixedNow.AddDays(-30));
        var newestId = await SeedContestAsync(finalizedUtc: FixedNow.AddDays(-1));

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IAuditContestEnrichment>(It.IsAny<Expression<Func<IAuditContestEnrichment, Task>>>()))
            .Callback<Expression<Func<IAuditContestEnrichment, Task>>>(expr =>
                enqueued.Add(AuditContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentAuditJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        enqueued.Should().Equal(oldestId, middleId, newestId);
    }

    [Fact]
    public async Task Execute_SkipsContestsAlreadyAudited()
    {
        var alreadyAuditedId = await SeedContestAsync(
            finalizedUtc: FixedNow.AddDays(-10),
            auditedUtc: FixedNow.AddDays(-1));
        var pendingId = await SeedContestAsync(finalizedUtc: FixedNow.AddDays(-5));

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IAuditContestEnrichment>(It.IsAny<Expression<Func<IAuditContestEnrichment, Task>>>()))
            .Callback<Expression<Func<IAuditContestEnrichment, Task>>>(expr =>
                enqueued.Add(AuditContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentAuditJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        enqueued.Should().ContainSingle().Which.Should().Be(pendingId);
        enqueued.Should().NotContain(alreadyAuditedId);
    }

    [Fact]
    public async Task Execute_SkipsContestsNotYetFinalized()
    {
        var unfinalizedId = await SeedContestAsync(finalizedUtc: null);
        var finalizedId = await SeedContestAsync(finalizedUtc: FixedNow.AddDays(-1));

        var enqueued = new List<Guid>();
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Setup(x => x.Enqueue<IAuditContestEnrichment>(It.IsAny<Expression<Func<IAuditContestEnrichment, Task>>>()))
            .Callback<Expression<Func<IAuditContestEnrichment, Task>>>(expr =>
                enqueued.Add(AuditContestIdFromExpression(expr) ?? Guid.Empty));

        var sut = Mocker.CreateInstance<ContestEnrichmentAuditJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        enqueued.Should().ContainSingle().Which.Should().Be(finalizedId);
        enqueued.Should().NotContain(unfinalizedId);
    }

    [Fact]
    public async Task Execute_NoCandidates_ReturnsCleanlyWithoutEnqueuing()
    {
        await SeedContestAsync(finalizedUtc: null);
        await SeedContestAsync(finalizedUtc: FixedNow.AddDays(-1), auditedUtc: FixedNow);

        var sut = Mocker.CreateInstance<ContestEnrichmentAuditJob<FootballDataContext>>();

        await sut.ExecuteAsync();

        Mocker.GetMock<IProvideBackgroundJobs>().Verify(
            x => x.Enqueue<IAuditContestEnrichment>(It.IsAny<Expression<Func<IAuditContestEnrichment, Task>>>()),
            Times.Never);
    }

    private async Task<Guid> SeedContestAsync(
        DateTime? finalizedUtc = null,
        DateTime? auditedUtc = null)
    {
        var contest = new FootballContest
        {
            Id = Guid.NewGuid(),
            Name = $"Contest {Guid.NewGuid():N}",
            ShortName = "TC",
            Sport = Sport.FootballNcaa,
            SeasonYear = 2025,
            StartDateUtc = FixedNow.AddDays(-30),
            HomeTeamFranchiseSeasonId = Guid.NewGuid(),
            AwayTeamFranchiseSeasonId = Guid.NewGuid(),
            FinalizedUtc = finalizedUtc,
            AuditedUtc = auditedUtc,
            CreatedUtc = FixedNow,
            CreatedBy = Guid.NewGuid()
        };

        await FootballDataContext.Contests.AddAsync(contest);
        await FootballDataContext.SaveChangesAsync();
        return contest.Id;
    }

    private static Guid? AuditContestIdFromExpression(
        Expression<Func<IAuditContestEnrichment, Task>> expr)
    {
        if (expr.Body is not MethodCallExpression call) return null;
        if (call.Method.Name != nameof(IAuditContestEnrichment.Process)) return null;
        if (call.Arguments.Count != 1) return null;

        var cmd = Expression.Lambda<Func<AuditContestEnrichmentCommand>>(call.Arguments[0]).Compile()();
        return cmd.ContestId;
    }
}
