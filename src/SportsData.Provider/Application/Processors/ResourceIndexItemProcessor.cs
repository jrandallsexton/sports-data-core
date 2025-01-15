using MassTransit;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

namespace SportsData.Provider.Application.Processors
{
    public interface IProcessResourceIndexes
    {
        Task Process(ProcessResourceIndexItemCommand command);
    }

    public class ResourceIndexItemProcessor : IProcessResourceIndexes
    {
        private readonly ILogger<ResourceIndexItemProcessor> _logger;
        private readonly IProvideEspnApiData _espnApi;
        private readonly DocumentService _documentService;
        private readonly IBus _bus;

        public ResourceIndexItemProcessor(
            ILogger<ResourceIndexItemProcessor> logger,
            IProvideEspnApiData espnApi,
            DocumentService documentService,
            IBus bus)
        {
            _logger = logger;
            _espnApi = espnApi;
            _documentService = documentService;
            _bus = bus;
        }

        public async Task Process(ProcessResourceIndexItemCommand command)
        {
            _logger.LogInformation("Started with {@command}", command);

            var type = GetType(command.SourceDataProvider, command.DocumentType);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(type.Name);

            // get the item's json
            var itemJson = await _espnApi.GetResource(command.Href, true);

            // determine if we have this in the database
            var documentId = command.SeasonYear.HasValue
                ? int.Parse($"{command.Id}{command.SeasonYear.Value}")
                : command.Id;

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, documentId);
            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();

            if (dbItem is null)
            {
                // no ?  save it and broadcast event
                await dbObjects.InsertOneAsync(new DocumentBase()
                {
                    Id = documentId,
                    Data = itemJson
                });

                var evt = new DocumentCreated()
                {
                    Id = documentId.ToString(),
                    Name = type.Name,
                    SourceDataProvider = command.SourceDataProvider,
                    DocumentType = command.DocumentType
                };
                await _bus.Publish(evt);
                _logger.LogInformation("New document event {@evt}", evt);
            }
            else
            {
                var dbJson = dbItem.ToJson();

                // has it changed ?
                if (string.Compare(itemJson, dbJson, StringComparison.InvariantCultureIgnoreCase) == 0)
                    return;

                // yes: update and broadcast
                await dbObjects.ReplaceOneAsync(filter, new DocumentBase()
                {
                    Id = documentId,
                    Data = itemJson
                });

                var evt = new DocumentUpdated()
                {
                    Id = documentId.ToString(),
                    Name = type.Name,
                    SourceDataProvider = command.SourceDataProvider,
                    DocumentType = command.DocumentType
                };
                await _bus.Publish(evt);

                _logger.LogInformation("Document updated event {@evt}", evt);
            }
        }

        private static Type GetType(SourceDataProvider sourceDataProvider, DocumentType docType)
        {
            switch (docType)
            {
                case DocumentType.Franchise:
                    return typeof(EspnFranchiseDto);
                case DocumentType.TeamBySeason:
                    return typeof(EspnTeamSeasonDto);
                case DocumentType.Athlete:
                case DocumentType.Award:
                case DocumentType.Event:
                case DocumentType.GameSummary:
                case DocumentType.Scoreboard:
                case DocumentType.Season:
                case DocumentType.Team:
                case DocumentType.TeamInformation:
                case DocumentType.Venue:
                case DocumentType.Weeks:
                default:
                    throw new ArgumentOutOfRangeException(nameof(docType), docType, null);
            }
        }
    }

    public record ProcessResourceIndexItemCommand
    {
        public int Id { get; init; }
        public string Href { get; init; }
        public SourceDataProvider SourceDataProvider { get; init; }
        public DocumentType DocumentType { get; init; }
        public int? SeasonYear { get; init; }
    }
}
