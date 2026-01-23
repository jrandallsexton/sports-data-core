using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeason)]
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.FranchiseSeasonLogo)]
    public class FranchiseSeasonLogoResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<FranchiseSeasonLogoResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public FranchiseSeasonLogoResponseProcessor(
            ILogger<FranchiseSeasonLogoResponseProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            var franchiseSeason = await _dataContext.FranchiseSeasons
                .AsNoTracking()
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (franchiseSeason is null)
            {
                // log and return
                _logger.LogError("franchiseSeason could not be found. Cannot process.");
                return;
            }

            // Check if logo with this OriginalUrlHash already exists for this FranchiseSeason
            var existingLogo = await _dataContext.FranchiseSeasonLogos
                .Where(x => x.Id.ToString() == response.ImageId)
                .FirstOrDefaultAsync();

            if (existingLogo is not null)
            {
                _logger.LogInformation(
                    "Updating existing FranchiseSeasonLogo. LogoId={LogoId}, FranchiseSeasonId={FranchiseSeasonId}, OriginalUrlHash={OriginalUrlHash}",
                    existingLogo.Id,
                    response.ParentEntityId,
                    response.OriginalUrlHash);

                // Update properties that may have changed
                existingLogo.Uri = response.Uri;
                existingLogo.Height = response.Height;
                existingLogo.Width = response.Width;
                existingLogo.Rel = response.Rel;
                existingLogo.ModifiedBy = response.CorrelationId;
                existingLogo.ModifiedUtc = _dateTimeProvider.UtcNow();

                _dataContext.FranchiseSeasonLogos.Update(existingLogo);
            }
            else
            {
                _logger.LogInformation(
                    "Creating new FranchiseSeasonLogo. ImageId={ImageId}, FranchiseSeasonId={FranchiseSeasonId}, OriginalUrlHash={OriginalUrlHash}",
                    response.ImageId,
                    response.ParentEntityId,
                    response.OriginalUrlHash);

                await _dataContext.FranchiseSeasonLogos.AddAsync(new FranchiseSeasonLogo()
                {
                    Id = Guid.Parse(response.ImageId),
                    FranchiseSeasonId = response.ParentEntityId,
                    CreatedBy = response.CorrelationId,
                    CreatedUtc = _dateTimeProvider.UtcNow(),
                    Uri = response.Uri,
                    Height = response.Height,
                    Width = response.Width,
                    OriginalUrlHash = response.OriginalUrlHash,
                    Rel = response.Rel
                });
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
