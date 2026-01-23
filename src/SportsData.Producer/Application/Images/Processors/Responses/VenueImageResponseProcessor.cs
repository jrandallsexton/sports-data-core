using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Venue)]
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.VenueImage)]
    public class VenueImageResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<VenueImageResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;

        public VenueImageResponseProcessor(
            ILogger<VenueImageResponseProcessor<TDataContext>> logger,
            TDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            _logger.LogInformation("ImageProcessedProcessor Began handler with {@response}", response);

            var venue = await _dataContext.Venues
                .AsNoTracking()
                .Include(x => x.Images)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (venue is null)
            {
                // log and return
                _logger.LogError("venue could not be found. Cannot process.");
                return;
            }

            var venueImage = venue.Images.FirstOrDefault(x => x.OriginalUrlHash == response.OriginalUrlHash);

            if (venueImage is not null)
            {
                // TODO: do nothing?
                return;
            }

            await _dataContext.VenueImages.AddAsync(new VenueImage()
            {
                Id = Guid.Parse(response.ImageId),
                VenueId = venue.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Uri = response.Uri,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel,
                OriginalUrlHash = response.OriginalUrlHash
            });

            await _dataContext.SaveChangesAsync();
        }
    }
}
