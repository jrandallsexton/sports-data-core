using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin;
using SportsData.Api.Application.Previews.Commands;
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

        public PreviewService(
            ILogger<PreviewService> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
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

            return preview.Id;
        }
    }
}
