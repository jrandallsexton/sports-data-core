using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteImage)]
    public class AthleteImageResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<AthleteImageResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public AthleteImageResponseProcessor(
            ILogger<AthleteImageResponseProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = response.CorrelationId
                   }))
            {
                _logger.LogInformation("Started with {@response}", response);
                await ProcessResponseInternal(response);
            }
        }

        private async Task ProcessResponseInternal(ProcessImageResponse response)
        {
            var parentEntity = await _dataContext.Athletes
                .Include(x => x.Images)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (parentEntity == null)
            {
                _logger.LogError("Athlete could not be found. Cannot process.");
                return;
            }

            var img = parentEntity.Images.FirstOrDefault(x => x.OriginalUrlHash == response.OriginalUrlHash);

            if (img is not null)
            {
                _logger.LogInformation(
                    "Updating existing AthleteImage. ImageId={ImageId}, AthleteId={AthleteId}, OriginalUrlHash={OriginalUrlHash}",
                    img.Id,
                    response.ParentEntityId,
                    response.OriginalUrlHash);

                // Update properties that may have changed
                // EF Core change tracker will detect only these modifications
                img.Uri = response.Uri;
                img.Height = response.Height;
                img.Width = response.Width;
                img.Rel = response.Rel;
                img.ModifiedBy = response.CorrelationId;
                img.ModifiedUtc = _dateTimeProvider.UtcNow();

                // No need to call Update() - change tracker handles it
                await _dataContext.SaveChangesAsync();

                _logger.LogInformation("AthleteImage updated successfully.");
                return;
            }

            // Validate ImageId before creating entity
            if (!Guid.TryParse(response.ImageId, out var imageId))
            {
                _logger.LogError(
                    "Invalid ImageId format detected. This indicates a bug in image request generation or data corruption. " +
                    "ImageId={ImageId}, ParentEntityId={ParentEntityId}, OriginalUrlHash={OriginalUrlHash}, Uri={Uri}. " +
                    "Skipping image processing to prevent consumer halt.",
                    response.ImageId,
                    response.ParentEntityId,
                    response.OriginalUrlHash,
                    response.Uri);
                return;
            }

            await _dataContext.AthleteImages.AddAsync(new AthleteImage()
            {
                Id = imageId,
                AthleteId = parentEntity.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = _dateTimeProvider.UtcNow(),
                Uri = response.Uri,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel,
                OriginalUrlHash = response.OriginalUrlHash
            });

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("AthleteImage created successfully.");
        }
    }
}
