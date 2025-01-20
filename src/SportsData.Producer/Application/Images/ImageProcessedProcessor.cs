using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

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
            var groupSeasonLogo = await _dataContext.GroupSeasonLogos
                .Where(l => l.Url == response.Url)
                .FirstOrDefaultAsync();

            if (groupSeasonLogo is null)
            {
                await _dataContext.GroupSeasonLogos.AddAsync(new GroupSeasonLogo()
                {
                    Id = Guid.Parse(response.ImageId),
                    GroupSeasonId = response.ParentEntityId,
                    CreatedBy = response.CorrelationId,
                    CreatedUtc = DateTime.UtcNow,
                    Url = response.Url,
                    Height = response.Height,
                    Width = response.Width
                });
                await _dataContext.SaveChangesAsync();
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
                Width = response.Width
            });
            await _dataContext.SaveChangesAsync();
        }
    }
}
