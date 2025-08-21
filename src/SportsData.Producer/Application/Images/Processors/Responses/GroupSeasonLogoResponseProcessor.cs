using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images.Processors.Responses
{
    [ImageResponseProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupSeasonLogo)]
    public class GroupSeasonLogoResponseProcessor<TDataContext> : IProcessLogoAndImageResponses
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<GroupSeasonLogoResponseProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IDecodeDocumentProvidersAndTypes _documentTypeDecoder;
        private readonly IEventBus _bus;
        private readonly IProvideProviders _providerClient;

        public GroupSeasonLogoResponseProcessor(
            ILogger<GroupSeasonLogoResponseProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IDecodeDocumentProvidersAndTypes documentTypeDecoder,
            IEventBus bus,
            IProvideProviders providerClient)
        {
            _logger = logger;
            _dataContext = dataContext;
            _documentTypeDecoder = documentTypeDecoder;
            _bus = bus;
            _providerClient = providerClient;
        }

        public async Task ProcessResponse(ProcessImageResponse response)
        {
            var groupSeason = await _dataContext.GroupSeasons
                .AsNoTracking()
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
                Uri = response.Uri,
                Height = response.Height,
                Width = response.Width,
                OriginalUrlHash = response.OriginalUrlHash
            });

            await _dataContext.SaveChangesAsync();
        }
    }
}
