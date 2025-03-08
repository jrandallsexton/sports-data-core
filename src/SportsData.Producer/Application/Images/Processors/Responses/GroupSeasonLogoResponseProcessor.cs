using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    public class GroupSeasonLogoResponseProcessor : IProcessLogoAndImageResponses
    {
        private readonly ILogger<GroupSeasonLogoResponseProcessor> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideHashes _hashProvider;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IPublishEndpoint _bus;
        private readonly IProvideProviders _providerClient;

        public GroupSeasonLogoResponseProcessor(
            ILogger<GroupSeasonLogoResponseProcessor> logger,
            TeamSportDataContext dataContext,
            IProvideHashes hashProvider,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IPublishEndpoint bus,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _hashProvider = hashProvider;
            _documentTypeDecoder = documentTypeDecoder;
            _bus = bus;
            _providerClient = providerClient;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
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
}
