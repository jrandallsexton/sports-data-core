using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    public class AthleteImageResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<AthleteImageResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;

        public AthleteImageResponseProcessor(
            ILogger<AthleteImageResponseProcessor<TDataContext>> logger,
            TDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            var correlationId = Guid.NewGuid();
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = correlationId
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
                // log and return
                _logger.LogError("Athlete could not be found. Cannot process.");
                return;
            }

            var img = parentEntity.Images.FirstOrDefault(x => x.OriginalUrlHash == response.OriginalUrlHash);

            if (img is not null)
            {
                // TODO: do nothing?
                return;
            }

            await _dataContext.AthleteImages.AddAsync(new AthleteImage()
            {
                Id = Guid.NewGuid(),
                AthleteId = parentEntity.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel
            });

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("AthleteImage created.");
        }
    }
}
