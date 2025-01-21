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

            switch (response.DocumentType)
            {
                case DocumentType.FranchiseLogo:
                    await ProcessFranchiseLogo(response);
                    break;
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
                case DocumentType.Franchise:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Venue:
                case DocumentType.Weeks:
                case DocumentType.GroupLogo:
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private async Task ProcessTeamBySeasonLogo(ProcessImageResponse response)
        {
            var franchiseSeasonLogo = await _dataContext.FranchiseSeasonLogos
                .Where(l => l.Url == response.Url)
                .FirstOrDefaultAsync();

            if (franchiseSeasonLogo is null)
            {
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
