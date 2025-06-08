using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupLogo)]
    public class GroupLogoResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<GroupLogoResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;

        public GroupLogoResponseProcessor(
            ILogger<GroupLogoResponseProcessor<TDataContext>> logger,
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
            var parentEntity = await _dataContext.Groups
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (parentEntity == null)
            {
                // log and return
                _logger.LogError("Group could not be found. Cannot process.");
                return;
            }

            var logo = parentEntity.Logos.FirstOrDefault(x => x.OriginalUrlHash == response.OriginalUrlHash);

            if (logo is not null)
            {
                // TODO: do nothing?
                return;
            }

            await _dataContext.GroupLogos.AddAsync(new GroupLogo()
            {
                Id = Guid.NewGuid(),
                GroupId = parentEntity.Id,
                CreatedBy = response.CorrelationId,
                CreatedUtc = DateTime.UtcNow,
                Url = response.Url,
                Height = response.Height,
                Width = response.Width,
                Rel = response.Rel
            });

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("GroupLogo created.");
        }
    }
}
