using FluentAssertions;

using Moq;

using SportsData.Api.Application.Previews.Commands.ApproveMatchupPreview;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;

using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.Previews.Commands.ApproveMatchupPreview;

/// <summary>
/// Approval changes what the cached league-week payload says about a contest
/// (IsPreviewReviewed), so it must evict the caches for that contest after the
/// write, and it drives StatBot's pick via MatchupPreviewApproved.
/// </summary>
public class ApproveMatchupPreviewCommandHandlerTests : ApiTestBase<ApproveMatchupPreviewCommandHandler>
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    public ApproveMatchupPreviewCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(Now);
    }

    [Fact]
    public async Task ExecuteAsync_StampsApproval_ThenEvictsForTheContest()
    {
        var (user, preview) = await SeedAsync();
        var sut = Mocker.CreateInstance<ApproveMatchupPreviewCommandHandler>();

        var result = await sut.ExecuteAsync(new ApproveMatchupPreviewCommand
        {
            PreviewId = preview.Id,
            ApprovedByUserId = user.Id
        });

        result.Should().BeOfType<Success<Guid>>();
        result.Value.Should().Be(preview.Id);
        var saved = (await DataContext.MatchupPreviews.FindAsync(preview.Id))!;
        saved.ApprovedUtc.Should().Be(Now);
        saved.ModifiedBy.Should().Be(user.Id);
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(preview.ContestId, It.IsAny<CancellationToken>()), Times.Once);
        // StatBot's pick follows the approved preview via this event.
        Mocker.GetMock<IEventBus>()
            .Verify(b => b.Publish(
                It.Is<MatchupPreviewApproved>(e => e.ContestId == preview.ContestId && e.MatchupPreviewId == preview.Id),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenPreviewMissing_ReturnsNotFound_AndDoesNotEvictOrPublish()
    {
        var (user, _) = await SeedAsync();
        var sut = Mocker.CreateInstance<ApproveMatchupPreviewCommandHandler>();

        var result = await sut.ExecuteAsync(new ApproveMatchupPreviewCommand
        {
            PreviewId = Guid.NewGuid(),
            ApprovedByUserId = user.Id
        });

        result.Should().BeOfType<Failure<Guid>>();
        result.Status.Should().Be(ResultStatus.NotFound);
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        Mocker.GetMock<IEventBus>()
            .Verify(b => b.Publish(It.IsAny<MatchupPreviewApproved>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserMissing_ReturnsNotFound_AndLeavesPreviewUnapproved()
    {
        var (_, preview) = await SeedAsync();
        var sut = Mocker.CreateInstance<ApproveMatchupPreviewCommandHandler>();

        var result = await sut.ExecuteAsync(new ApproveMatchupPreviewCommand
        {
            PreviewId = preview.Id,
            ApprovedByUserId = Guid.NewGuid()
        });

        result.Should().BeOfType<Failure<Guid>>();
        result.Status.Should().Be(ResultStatus.NotFound);
        (await DataContext.MatchupPreviews.FindAsync(preview.Id))!.ApprovedUtc.Should().BeNull();
    }

    private async Task<(UserEntity user, MatchupPreview preview)> SeedAsync()
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            FirebaseUid = "uid",
            Email = "admin@example.com",
            SignInProvider = "google",
            DisplayName = "Admin",
            Username = "admin",
            IsAdmin = true,
            LastLoginUtc = Now,
            CreatedUtc = Now,
            CreatedBy = Guid.Empty
        };
        var preview = new MatchupPreview
        {
            Id = Guid.NewGuid(),
            ContestId = Guid.NewGuid(),
            PromptId = Guid.NewGuid(),
            CreatedUtc = Now,
            CreatedBy = Guid.Empty
        };
        DataContext.Users.Add(user);
        DataContext.MatchupPreviews.Add(preview);
        await DataContext.SaveChangesAsync();
        return (user, preview);
    }
}
