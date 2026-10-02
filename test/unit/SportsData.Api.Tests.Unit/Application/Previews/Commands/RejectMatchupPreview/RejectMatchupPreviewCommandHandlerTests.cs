using FluentAssertions;

using Moq;

using SportsData.Api.Application.Previews.Commands.RejectMatchupPreview;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using System;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.Previews.Commands.RejectMatchupPreview;

/// <summary>
/// Rejection changes what the cached league-week payload says about a contest
/// (IsPreviewReviewed and IsPreviewAvailable both derive from RejectedUtc), so
/// it must evict the caches for that contest after the write.
/// </summary>
public class RejectMatchupPreviewCommandHandlerTests : ApiTestBase<RejectMatchupPreviewCommandHandler>
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    public RejectMatchupPreviewCommandHandlerTests()
    {
        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(Now);
    }

    [Fact]
    public async Task ExecuteAsync_StampsRejection_ThenEvictsForTheContest()
    {
        var (user, preview) = await SeedAsync();
        var sut = Mocker.CreateInstance<RejectMatchupPreviewCommandHandler>();

        var result = await sut.ExecuteAsync(new RejectMatchupPreviewCommand
        {
            PreviewId = preview.Id,
            ContestId = preview.ContestId,
            RejectionNote = "wrong favorite",
            RejectedByUserId = user.Id
        });

        result.Should().BeOfType<Success<Guid>>();
        result.Value.Should().Be(preview.Id);
        var saved = (await DataContext.MatchupPreviews.FindAsync(preview.Id))!;
        saved.RejectedUtc.Should().Be(Now);
        saved.RejectionNote.Should().Be("wrong favorite");
        saved.ModifiedBy.Should().Be(user.Id);
        Mocker.GetMock<ILeagueWeekMatchupsCacheInvalidator>()
            .Verify(i => i.EvictForContestAsync(preview.ContestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WhenContestDoesNotMatchPreview_ReturnsNotFound_AndDoesNotEvict()
    {
        var (user, preview) = await SeedAsync();
        var sut = Mocker.CreateInstance<RejectMatchupPreviewCommandHandler>();

        var result = await sut.ExecuteAsync(new RejectMatchupPreviewCommand
        {
            PreviewId = preview.Id,
            ContestId = Guid.NewGuid(),
            RejectionNote = "wrong favorite",
            RejectedByUserId = user.Id
        });

        result.Should().BeOfType<Failure<Guid>>();
        result.Status.Should().Be(ResultStatus.NotFound);
        (await DataContext.MatchupPreviews.FindAsync(preview.Id))!.RejectedUtc.Should().BeNull();
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
