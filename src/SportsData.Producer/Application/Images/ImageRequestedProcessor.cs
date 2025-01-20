using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Images
{
    public interface IProcessImageRequests
    {
        Task Process(ProcessImageRequest request);
    }

    public class ImageRequestedProcessor : IProcessImageRequests
    {
        private readonly ILogger<ImageRequestedProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBlobStorage _blobStorage;
        private readonly IBus _bus;

        public ImageRequestedProcessor(
            ILogger<ImageRequestedProcessor> logger,
            AppDataContext dataContext,
            IBus bus,
            IProvideBlobStorage blobStorage)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _blobStorage = blobStorage;
        }

        public async Task Process(ProcessImageRequest request)
        {
            _logger.LogInformation("Began with {@request}", request);

            var logoEntity = await GetLogoEntity(request.DocumentType, request.ParentEntityId);
            ProcessImageResponse outgoingEvt;

            if (logoEntity != null)
            {
                // if so, just create the event
                outgoingEvt = new ProcessImageResponse(
                    logoEntity.Url,
                    logoEntity.Id.ToString(),
                    request.ParentEntityId,
                    request.Name,
                    request.Sport,
                    request.SeasonYear,
                    request.DocumentType,
                    request.SourceDataProvider,
                    request.Height,
                    request.Width,
                    request.Rel);
            }
            else
            {
                // if not, obtain the image
                using var client = new HttpClient();
                using var response = await client.GetAsync(request.Url);
                await using var stream = await response.Content.ReadAsStreamAsync();

                // upload it to external storage
                var containerName = request.SeasonYear.HasValue
                    ? $"{request.Sport}{request.DocumentType.ToString()}{request.SeasonYear.Value}"
                    : $"{request.Sport}{request.DocumentType.ToString()}";
                var externalUrl = await _blobStorage.UploadImageAsync(stream, containerName, $"{request.ImageId}.png");

                // raise an event for whoever requested this
                outgoingEvt = new ProcessImageResponse(
                    externalUrl,
                    request.ImageId.ToString(),
                    request.ParentEntityId,
                    request.Name,
                    request.Sport,
                    request.SeasonYear,
                    request.DocumentType,
                    request.SourceDataProvider,
                    request.Height,
                    request.Width,
                    request.Rel);
            }

            await _bus.Publish(outgoingEvt);
        }

        private async Task<ILogo?> GetLogoEntity(DocumentType documentType, Guid parentEntityId)
        {
            switch (documentType)
            {
                case DocumentType.FranchiseLogo:
                    return await _dataContext.FranchiseLogos
                        .Where(l => l.FranchiseId == parentEntityId)
                        .FirstOrDefaultAsync();
                case DocumentType.GroupBySeason:
                    return await _dataContext.GroupSeasonLogos
                        .Where(l => l.GroupSeasonId == parentEntityId)
                        .FirstOrDefaultAsync();
                case DocumentType.GroupLogo:
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
                case DocumentType.TeamBySeason:
                case DocumentType.TeamInformation:
                case DocumentType.Venue:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(documentType), documentType, null);
            }
        }
    }
}
