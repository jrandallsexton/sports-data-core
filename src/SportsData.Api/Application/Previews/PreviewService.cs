using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews.Commands;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.Previews
{
    public interface IPreviewService
    {
        Task<Guid> ApproveMatchupPreview(ApproveMatchupPreviewCommand command);
        Task<Guid> RejectMatchupPreview(RejectMatchupPreviewCommand command);
    }

    public class PreviewService : IPreviewService
    {
        private readonly ILogger<PreviewService> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ILeagueWeekMatchupsCacheInvalidator _cacheInvalidator;

        public PreviewService(
            ILogger<PreviewService> logger,
            AppDataContext dataContext,
            ILeagueWeekMatchupsCacheInvalidator cacheInvalidator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _cacheInvalidator = cacheInvalidator;
        }

        public async Task<Guid> ApproveMatchupPreview(ApproveMatchupPreviewCommand command)
        {
            var user = await _dataContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == command.ApprovedByUserId);

            if (user is null)
            {
                throw new InvalidOperationException("User not found");
            }

            //if (!user.IsAdmin)
            //{
            //    throw new UnauthorizedAccessException("User is not an admin");
            //}
            var preview = await _dataContext.MatchupPreviews
                .FirstOrDefaultAsync(x => x.Id == command.PreviewId);

            if (preview is null)
                throw new InvalidOperationException("Preview not found.");

            preview.ApprovedUtc = DateTime.UtcNow;
            preview.ModifiedBy = command.ApprovedByUserId;

            await _dataContext.SaveChangesAsync();

            // IsPreviewReviewed lives in the cached league-week payload.
            await _cacheInvalidator.EvictForContestAsync(preview.ContestId);

            return preview.Id;
        }

        public async Task<Guid> RejectMatchupPreview(RejectMatchupPreviewCommand command)
        {
            var user = await _dataContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == command.RejectedByUserId);

            if (user is null)
            {
                throw new InvalidOperationException("User not found");
            }

            //if (!user.)
            //{
            //    throw new UnauthorizedAccessException("User is not an admin");
            //}

            var preview = await _dataContext.MatchupPreviews
                .FirstOrDefaultAsync(x => x.Id == command.PreviewId &&
                                          x.ContestId == command.ContestId);

            if (preview is null)
                throw new InvalidOperationException("Preview not found.");

            preview.RejectedUtc = DateTime.UtcNow;
            preview.RejectionNote = command.RejectionNote;
            preview.ModifiedBy = command.RejectedByUserId;

            await _dataContext.SaveChangesAsync();

            // A rejected preview is no longer "available" in the cached league-week
            // payload either - both flags there derive from RejectedUtc.
            await _cacheInvalidator.EvictForContestAsync(preview.ContestId);

            return preview.Id;
        }
    }
}
