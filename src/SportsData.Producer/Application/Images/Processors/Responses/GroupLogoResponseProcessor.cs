using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Images;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    public class GroupLogoResponseProcessor : IProcessLogoAndImageResponses
    {
        private readonly ILogger<GroupLogoResponseProcessor> _logger;
        private readonly AppDataContext _dataContext;

        public GroupLogoResponseProcessor(
            ILogger<GroupLogoResponseProcessor> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            var group = await _dataContext.Groups
                .Include(x => x.Logos)
                .Where(x => x.Id == response.ParentEntityId)
                .FirstOrDefaultAsync();

            if (group == null)
            {
                // log and return
                _logger.LogError("group could not be found. Cannot process.");
                return;
            }

            var logo = group.Logos.FirstOrDefault(x => x.OriginalUrlHash == response.OriginalUrlHash);

            if (logo is not null)
            {
                // TODO: do nothing?
                return;
            }

            await _dataContext.GroupLogos.AddAsync(new GroupLogo()
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
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
