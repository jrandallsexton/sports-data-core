using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
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
            using (_logger.BeginScope(new Dictionary<string, Guid>()
                   {
                       { "CorrelationId", response.CorrelationId }
                   }))

            await ProcessResponse(response);
        }

        private async Task ProcessResponse(ProcessImageResponse response)
        {
            switch (response.DocumentType)
            {
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    await ProcessFranchiseLogo(response);
                    break;
                case DocumentType.GroupLogo:
                case DocumentType.GroupBySeason:
                case DocumentType.GroupBySeasonLogo:
                    await ProcessGroupBySeasonLogo(response);
                    break;
                case DocumentType.TeamBySeason:
                case DocumentType.TeamBySeasonLogo:
                    await ProcessTeamBySeasonLogo(response);
                    break;
                case DocumentType.Venue:
                case DocumentType.VenueImage:
                    await ProcessVenueImage(response);
                    break;
                case DocumentType.Athlete:
                case DocumentType.AthleteBySeason:
                case DocumentType.Award:
                case DocumentType.CoachBySeason:
                case DocumentType.Contest:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task ProcessVenueImage(ProcessImageResponse response)
        {
            _logger.LogInformation("ImageProcessedProcessor Began handler with {@response}", response);
            
            var venue = await _dataContext.Venues
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

            // TODO: Prefer to add this to venue, but kept getting the following error:
            // The database operation was expected to affect 1 row(s), but actually affected 0 row(s)
            await _dataContext.VenueImages.AddAsync(new VenueImage()
            {
                Id = Guid.NewGuid(),
                VenueId = venue.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel,
                OriginalUrlHash = response.OriginalUrlHash
            });

            await _dataContext.SaveChangesAsync();
        }

        private async Task ProcessTeamBySeasonLogo(ProcessImageResponse response)
        {
            var teamBySeason = await _dataContext.FranchiseSeasons
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (teamBySeason is null)
            {
                // log and return
                _logger.LogError("teamBySeason could not be found. Cannot process.");
                return;
            }

            teamBySeason.Logos.Add(new FranchiseSeasonLogo()
            {
                Id = Guid.Parse(response.ImageId),
                FranchiseSeasonId = response.ParentEntityId,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width
            });

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to process teamBySeason logo for {@response}. EntityId: {@franchiseId}", response, teamBySeason.Id);
            }
        }

        private async Task ProcessGroupBySeasonLogo(ProcessImageResponse response)
        {
            var groupSeason = await _dataContext.GroupSeasons
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (groupSeason is null)
            {
                // log and return
                _logger.LogError("groupSeason could not be found. Cannot process.");
                return;
            }

            groupSeason.Logos.Add(new GroupSeasonLogo()
            {
                Id = Guid.Parse(response.ImageId),
                GroupSeasonId = response.ParentEntityId,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width
            });

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to process groupBySeason logo for {@response}. EntityId: {@franchiseId}", response, groupSeason.Id);
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
                FranchiseId = franchise.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel
            });

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unable to process franchise logo for {@response}. EntityId: {@franchiseId}", response, franchise.Id);
            }
        }
    }
}
