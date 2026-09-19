using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Previews.Commands;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Previews;

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
        private readonly IEventBus _eventBus;

        public PreviewService(
            ILogger<PreviewService> logger,
            AppDataContext dataContext,
            ILeagueWeekMatchupsCacheInvalidator cacheInvalidator,
            IEventBus eventBus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _cacheInvalidator = cacheInvalidator;
            _eventBus = eventBus;
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

            // StatBot's pick follows the approved preview (MatchupPreviewApprovedHandler).
            // Published BEFORE SaveChanges so the outbox captures it in the same
            // transaction as the approval. Sport comes from a league carrying the
            // contest; previews exist only for football today, so NCAA is the
            // fallback when no league has it yet.
            var sport = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.ContestId == preview.ContestId)
                .Join(_dataContext.PickemGroups.AsNoTracking(), m => m.GroupId, g => g.Id, (m, g) => (Sport?)g.Sport)
                .FirstOrDefaultAsync() ?? Sport.FootballNcaa;

            await _eventBus.Publish(new MatchupPreviewApproved(
                MatchupPreviewId: preview.Id,
                ContestId: preview.ContestId,
                Ref: null,
                Sport: sport,
                SeasonYear: null,
                CorrelationId: Guid.NewGuid(),
                CausationId: CausationId.Api.PreviewApproval));

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
