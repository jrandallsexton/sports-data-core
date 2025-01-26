using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    public class FranchiseSeasonLogoResponseProcessor : IProcessLogoAndImageResponses
    {
        private readonly ILogger<FranchiseSeasonLogoResponseProcessor> _logger;
        private readonly AppDataContext _dataContext;

        public FranchiseSeasonLogoResponseProcessor(
            ILogger<FranchiseSeasonLogoResponseProcessor> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            var franchiseSeason = await _dataContext.FranchiseSeasons
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (franchiseSeason is null)
            {
                // log and return
                _logger.LogError("franchiseSeason could not be found. Cannot process.");
                return;
            }

            await _dataContext.FranchiseSeasonLogos.AddAsync(new FranchiseSeasonLogo()
            {
                Id = Guid.Parse(response.ImageId),
                FranchiseSeasonId = response.ParentEntityId,
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
