using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Migrations;

namespace SportsData.Producer.Application.Images
{
    public interface IProcessProcessedImages
    {
        Task Process(ProcessImageResponse response);
    }

    public class ImageProcessedProcessor : IProcessProcessedImages
    {
        private readonly ILogger<ImageProcessedProcessor> _logger;
        private readonly AppDataContext _dataContext;

        public ImageProcessedProcessor(
            ILogger<ImageProcessedProcessor> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task Process(ProcessImageResponse response)
        {
            _logger.LogInformation("Began with {@evt}", response);

            await ProcessGroupBySeasonLogo(response);
        }

        private async Task ProcessGroupBySeasonLogo(ProcessImageResponse response)
        {
            var group = await _dataContext.Groups
                .Include(g => g.Seasons)
                .ThenInclude(s => s.Logos)
                .Where(g => g.Id == response.ParentEntityId)
            .FirstOrDefaultAsync();

            if (group == null)
            {
                // log and return
                _logger.LogError("GroupSeason could not be found. Cannot process.");
                return;
            }
        }

        private async Task ProcessFranchiseLogo(ProcessImageResponse response)
        {
            var franchise = await _dataContext.Franchises
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (franchise == null)
            {
                // log and return
                _logger.LogError("Franchise could not be found. Cannot process.");
                return;
            }

            franchise.Logos.Add(new FranchiseLogo()
            {
                Id = Guid.NewGuid(),
                FranchiseId = response.ParentEntityId,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width,

            });
            await _dataContext.SaveChangesAsync();
        }
    }
}
