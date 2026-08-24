using AutoFixture;

using FluentAssertions;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

/// <summary>
/// Invalidation is what keeps the audit watermark honest: the nightly audit
/// only looks at picks with a null AuditedUtc, so a corrected contest that is
/// never invalidated stays invisible forever.
/// </summary>
public class PickAuditInvalidatorTests : ApiTestBase<PickAuditInvalidator>
{
    private static readonly DateTime Audited =
        new(2026, 8, 24, 2, 0, 0, DateTimeKind.Utc);

    private PickemGroupUserPick SeedPick(Guid contestId, DateTime? auditedUtc)
    {
        var pick = Fixture.Build<PickemGroupUserPick>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.ContestId, contestId)
            .With(x => x.ScoredAt, (DateTime?)new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            .With(x => x.AuditedUtc, auditedUtc)
            .Create();

        DataContext.UserPicks.Add(pick);
        return pick;
    }

    [Fact]
    public async Task InvalidateForContest_ClearsTheWatermark_SoTheNightlyAuditPicksItUpAgain()
    {
        var contestId = Guid.NewGuid();
        var pick = SeedPick(contestId, Audited);
        await DataContext.SaveChangesAsync();

        var count = await Mocker.CreateInstance<PickAuditInvalidator>()
            .InvalidateForContestAsync(contestId, "ContestScoreChanged");

        count.Should().Be(1);
        pick.AuditedUtc.Should().BeNull();
    }

    [Fact]
    public async Task InvalidateForContest_LeavesOtherContestsAlone()
    {
        var target = Guid.NewGuid();
        var bystander = Guid.NewGuid();
        SeedPick(target, Audited);
        var untouched = SeedPick(bystander, Audited);
        await DataContext.SaveChangesAsync();

        await Mocker.CreateInstance<PickAuditInvalidator>()
            .InvalidateForContestAsync(target, "ContestFinalized");

        untouched.AuditedUtc.Should().Be(Audited);
    }

    [Fact]
    public async Task InvalidateForContest_IsANoOp_WhenNothingWasAudited()
    {
        // The common case by far: a contest finalizing for the first time has
        // no audited picks, and this must not churn writes for it.
        var contestId = Guid.NewGuid();
        SeedPick(contestId, null);
        await DataContext.SaveChangesAsync();

        var count = await Mocker.CreateInstance<PickAuditInvalidator>()
            .InvalidateForContestAsync(contestId, "ContestFinalized");

        count.Should().Be(0);
    }

    [Fact]
    public async Task InvalidateForContest_DoesNotTouchScoring()
    {
        // Only the watermark clears — the pick stays scored, because the
        // audit's job is to VERIFY the scoring, not to discard it.
        var contestId = Guid.NewGuid();
        var pick = SeedPick(contestId, Audited);
        var scoredAt = pick.ScoredAt;
        await DataContext.SaveChangesAsync();

        await Mocker.CreateInstance<PickAuditInvalidator>()
            .InvalidateForContestAsync(contestId, "ContestScoreChanged");

        pick.ScoredAt.Should().Be(scoredAt);
        pick.AuditedUtc.Should().BeNull();
    }
}
