using MassTransit;

using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.TeamInformation;

namespace SportsData.Provider.Application.Documents
{
    // TODO: Move everything here into cqrs. this should be clean.

    [Route("api/document")]
    public class DocumentController : ApiControllerBase
    {
        private readonly ILogger<DocumentController> _logger;
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IBus _bus;

        public DocumentController(
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder, IBus bus, ILogger<DocumentController> logger)
        {
            _documentService = documentService;
            _decoder = decoder;
            _bus = bus;
            _logger = logger;
        }

        [HttpGet("{providerId}/{sportId}/{typeId}/{documentId}/{seasonId}")]
        public async Task<IActionResult> GetDocument(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            int documentId,
            int? seasonId)
        {
            var typeAndName = _decoder.GetTypeAndName(providerId, sportId, typeId, seasonId);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(typeAndName.Name);

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, documentId);

            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();

            // TODO: Clean this up
            return dbItem != null ? Ok(dbItem.Data) : NotFound();
        }

        [HttpPost("publish", Name = "PublishDocumentEvents")]
        public async Task<IActionResult> PublishDocumentEvents([FromBody]PublishDocumentEventsCommand command)
        {
            var typeAndName = _decoder.GetTypeAndName(
                command.SourceDataProvider,
                command.Sport,
                command.DocumentType,
                command.Season);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(typeAndName.Name);

            // https://www.mongodb.com/docs/drivers/csharp/current/fundamentals/crud/read-operations/retrieve/
            var filter = Builders<DocumentBase>.Filter.Empty;
            var dbCursor = await dbObjects.FindAsync(filter);
            var dbDocuments = await dbCursor.ToListAsync();

            var events = dbDocuments.Select(tmp =>
                new DocumentCreated(
                    tmp.Id.ToString(),
                    typeAndName.Type.Name,
                    command.Sport,
                    command.Season,
                    command.DocumentType,
                    command.SourceDataProvider)).ToList();

            await _bus.PublishBatch(events);

            _logger.LogInformation($"Published {events.Count} events.");

            return Ok();
        }
        
    }
}
