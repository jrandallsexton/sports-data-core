using FluentAssertions;

using Moq;

using SportsData.Api.Application.Previews;
using SportsData.Api.Application.Previews.Commands;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.Previews;

/// <summary>
/// Approve and reject both change what the cached league-week payload says about
/// a contest (IsPreviewReviewed; reject also clears IsPreviewAvailable), so both
/// must evict the caches for that contest after the write.
/// </summary>
public class PreviewServiceTests : ApiTestBase<PreviewService>
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ApproveMatchupPreview_StampsApproval_ThenEvictsForTheContest()
    {
        var (user, preview) = await SeedAsync();
        var sut = Mocker.CreateInstance<PreviewService>();

        var id = await sut.ApproveMatchupPreview(new ApproveMatchupPreviewCommand
        {
            PreviewId = preview.Id,
            ApprovedByUserId = user.Id
        });

        id.Should().Be(preview.Id);
        (await DataContext.MatchupPreviews.FindAsync(preview.Id))!.ApprovedUtc.Should().NotBeNull();
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(preview.ContestId, It.IsAny<CancellationToken>()), Times.Once);
        // StatBot's pick follows the approved preview via this event.
        Mocker.GetMock<IEventBus>()
            .Verify(b => b.Publish(
                It.Is<MatchupPreviewApproved>(e => e.ContestId == preview.ContestId && e.MatchupPreviewId == preview.Id),
                It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RejectMatchupPreview_StampsRejection_ThenEvictsForTheContest()
    {
        var (user, preview) = await SeedAsync();
        var sut = Mocker.CreateInstance<PreviewService>();

        var id = await sut.RejectMatchupPreview(new RejectMatchupPreviewCommand
        {
            PreviewId = preview.Id,
            ContestId = preview.ContestId,
            RejectionNote = "wrong favorite",
            RejectedByUserId = user.Id
        });

        id.Should().Be(preview.Id);
        (await DataContext.MatchupPreviews.FindAsync(preview.Id))!.RejectedUtc.Should().NotBeNull();
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(preview.ContestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveMatchupPreview_WhenPreviewMissing_DoesNotEvict()
    {
        var (user, _) = await SeedAsync();
        var sut = Mocker.CreateInstance<PreviewService>();

        var act = () => sut.ApproveMatchupPreview(new ApproveMatchupPreviewCommand
        {
            PreviewId = Guid.NewGuid(),
            ApprovedByUserId = user.Id
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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
