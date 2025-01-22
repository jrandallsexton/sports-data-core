using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
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

            switch (response.DocumentType)
            {
                case DocumentType.Franchise:
                case DocumentType.FranchiseLogo:
                    await ProcessFranchiseLogo(response);
                    break;
                case DocumentType.GroupLogo:
                case DocumentType.GroupBySeason:
                    await ProcessGroupBySeasonLogo(response);
                    break;
                case DocumentType.TeamBySeason:
                    await ProcessTeamBySeasonLogo(response);
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
                case DocumentType.Venue:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException();
            }
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
