using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    public class FranchiseLogoResponseProcessor : IProcessLogoAndImageResponses
    {
        private readonly ILogger<FranchiseLogoResponseProcessor> _logger;
        private readonly TeamSportDataContext _dataContext;

        public FranchiseLogoResponseProcessor(
            ILogger<FranchiseLogoResponseProcessor> logger,
            TeamSportDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
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

            var franchiseLogo = franchise.Logos.FirstOrDefault(x => x.OriginalUrlHash == response.OriginalUrlHash);

            if (franchiseLogo is not null)
            {
                // TODO: do nothing?
                return;
            }

            await _dataContext.FranchiseLogos.AddAsync(new FranchiseLogo()
            {
                Id = Guid.NewGuid(),
                FranchiseId = franchise.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel
            });

            await _dataContext.SaveChangesAsync();
        }
    }
}
