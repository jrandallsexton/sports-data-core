using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamInformation)]
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeason)]
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamBySeasonLogo)]
    public class FranchiseSeasonLogoResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<FranchiseSeasonLogoResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;

        public FranchiseSeasonLogoResponseProcessor(
            ILogger<FranchiseSeasonLogoResponseProcessor<TDataContext>> logger,
            TDataContext dataContext)
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
                Uri = response.Uri,
                Height = response.Height,
                Width = response.Width,
                OriginalUrlHash = response.OriginalUrlHash
            });

            await _dataContext.SaveChangesAsync();
        }
    }
}
