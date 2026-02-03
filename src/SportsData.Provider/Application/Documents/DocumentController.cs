using MassTransit;

using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Routing;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Application.Documents
{
    // TODO: Move everything here into cqrs. this should be clean.

    [Route("api/document")]
    public class DocumentController : ApiControllerBase
    {
        private readonly ILogger<DocumentController> _logger;
        private readonly IDocumentStore _documentStore;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IProvideBlobStorage _blobStorage;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IGenerateRoutingKeys _routingKeyGenerator;
        private readonly IBus _bus;
        private readonly IHttpClientFactory _clientFactory;
        private readonly EspnHttpClient _espnHttpClient;
        private readonly IHostEnvironment _environment;
        private readonly IAppMode _appMode;

        public DocumentController(
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            ILogger<DocumentController> logger,
            IProvideBlobStorage blobStorage,
            IProvideBackgroundJobs backgroundJobProvider,
            IGenerateRoutingKeys routingKeyGenerator,
            IBus bus,
            IHttpClientFactory clientFactory,
            EspnHttpClient espnHttpClient,
            IHostEnvironment environment,
            IAppMode appMode)
        {
            _documentStore = documentStore;
            _decoder = decoder;
            _logger = logger;
            _blobStorage = blobStorage;
            _backgroundJobProvider = backgroundJobProvider;
            _routingKeyGenerator = routingKeyGenerator;
            _bus = bus;
            _clientFactory = clientFactory;
            _espnHttpClient = espnHttpClient;
            _environment = environment;
            _appMode = appMode;
        }

        [HttpGet("urlHash/{hash}")]
        public async Task<IActionResult> GetDocumentByUrlHash(string hash)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["SourceUrlHash"] = hash }))
            {
                var collectionName = _appMode.CurrentSport.ToString();

                _logger.LogDebug("Collection name decoded {@CollectionName}", collectionName);

                var dbItem = await _documentStore
                    .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.SourceUrlHash == hash);

                _logger.LogDebug(dbItem == null ? "No document found" : "Document found");

                return dbItem != null ? Ok(dbItem.Data) : NotFound("Document not found");
            }
        }

        [HttpGet("{providerId}/{sportId}/{typeId}/{documentId}")]
        public async Task<IActionResult> GetDocument(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            long documentId)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["SourceDataProvider"] = providerId,
                       ["Sport"] = sportId,
                       ["DocumentType"] = typeId,
                       ["DocumentId"] = documentId,
            }))
            {
                var collectionName = _decoder.GetCollectionName(providerId, sportId, typeId, null);

                _logger.LogDebug("Collection name decoded {@CollectionName}", collectionName);

                var dbItem = await _documentStore
                    .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.id == documentId.ToString());

                _logger.LogInformation(dbItem == null ? "No document found" : "Document found");

                // TODO: Clean this up
                return dbItem != null ? Ok(dbItem.Data) : NotFound("Document not found");
            }
        }

        [HttpGet("{providerId}/{sportId}/{typeId}/{documentId}/{seasonId}")]
        public async Task<IActionResult> GetDocumentBySeason(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            long documentId,
            int seasonId)
        {
            var collectionName = _decoder.GetCollectionName(providerId, sportId, typeId, seasonId);

            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.Id == documentId.ToString());

            // TODO: Clean this up
            return dbItem != null ? Ok(dbItem.Data) : NotFound("Document not found");
        }

        [HttpGet("{providerId}/{externalUrl}")]
        public async Task<IActionResult> ProcessResourceIndex([FromBody] ProcessResourceIndexCommand command)
        {
            await Task.Delay(100);
            throw new NotImplementedException();
            // Check to see if the document is in the database

            // if so?  return it

            // if not
            // TODO: Get the correct providerClient (for now only ESPN)

        }

        /// <summary>
        /// Publishes events for each object in the database that currently exists; do not re-fetch from external
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        [HttpPost("publish", Name = "PublishDocumentEvents")]
        public IActionResult PublishDocumentEvents([FromBody]PublishDocumentEventsCommand command)
        {
            _backgroundJobProvider.Enqueue<PublishDocumentEventsProcessor>(x => x.Process(command));
            return Accepted();
        }

        [HttpPost("external/document", Name = "GetExternalDocument")]
        public async Task<ActionResult<GetExternalDocumentResponse>> GetExternalDocument(
            [FromBody] GetExternalDocumentQuery query)
        {
            _logger.LogInformation("Began with {@Query}", query);

            var collectionName = _decoder.GetCollectionName(
                query.SourceDataProvider,
                query.Sport,
                query.DocumentType,
                query.SeasonYear);

            var uriToUse = EspnRequestUri.ForFetch(query.Uri);

            _logger.LogDebug("Collection name decoded {@CollectionName}", collectionName);

            // generate a hash for the collection retrieval
            var hash = HashProvider.GenerateHashFromUri(uriToUse);

            _logger.LogInformation("Hash generated {@Hash}", hash);

            // Look for the item in the database first to see if we already have a link to it in blob storage
            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.Id == hash.ToString());

            if (dbItem is not null)
            {
                return Ok(new GetExternalDocumentResponse()
                {
                    Id = hash,
                    CanonicalId = query.CanonicalId ?? string.Empty,
                    Uri = dbItem.Uri,
                    IsSuccess = true,
                    Data = dbItem.Data
                });
            }

            // get the item via the url
            using var client = new HttpClient();
            using var response = await client.GetAsync(uriToUse);
            var json = await response.Content.ReadAsStringAsync();

            // save a record for the hash and blob url
            await _documentStore.InsertOneAsync(collectionName, new DocumentBase()
            {
                Id = hash,
                Data = json,
                DocumentType = query.DocumentType,
                RoutingKey = _routingKeyGenerator.Generate(query.SourceDataProvider, uriToUse),
                Uri = uriToUse,
                SourceUrlHash = hash,
                SourceDataProvider = query.SourceDataProvider,
                Sport = query.Sport
            });

            _logger.LogInformation("Document saved to database");

            // return the url generated by our blob storage provider
            return Ok(new GetExternalDocumentResponse()
            {
                Id = hash,
                CanonicalId = query.CanonicalId ?? string.Empty,
                Uri = uriToUse,
                Data = json
            });
        }

        /// <summary>
        /// Gets the blob storage url for an image, or fetches it, uploads to blob, and returns the canonical url
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        [HttpPost("external/image", Name = "GetExternalImage")]
        public async Task<ActionResult<GetExternalImageResponse>> GetExternalImage(
            [FromBody] GetExternalImageQuery query, CancellationToken ct)
        {
            _logger.LogInformation("Began with {@Query}", query);

            var collectionName = _decoder.GetCollectionName(
                query.SourceDataProvider,
                query.Sport,
                query.DocumentType,
                query.SeasonYear);

            _logger.LogDebug("Collection name decoded {@CollectionName}", collectionName);

            // generate a hash for the collection retrieval
            var hash = HashProvider.GenerateHashFromUri(query.Uri);
            var host = query.Uri.Host;

            _logger.LogInformation("Hash generated {@Hash}", hash);

            // Look for the item in the database first to see if we already have a link to it in blob storage
            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.Id == hash);

            if (dbItem is not null)
            {
                return Ok(new GetExternalImageResponse()
                {
                    Id = hash,
                    CanonicalId = query.CanonicalId,
                    Uri = dbItem.Uri,
                    IsSuccess = true
                });
            }

            // TODO: Do this better
            var bypassCache = (Environment.MachineName != "BENDER");

            // get the image (from cache or ESPN)
            await using var stream = await _espnHttpClient.GetCachedImageStreamAsync(
                query.Uri,
                bypassCache: bypassCache,
                stripQuerystring: true,
                extension: "png", ct);

            if (stream is null)
            {
                _logger.LogWarning("Image fetch failed {Host} {Hash}", host, hash);
                return StatusCode(502, "Failed to fetch image.");
            }

            // upload it to blob storage
            var containerName = query.SeasonYear.HasValue
                ? $"{query.DocumentType.ToKebabCase()}-{query.Sport.ToKebabCase()}-{query.SeasonYear.Value}"
                : $"{query.DocumentType.ToKebabCase()}-{query.Sport.ToKebabCase()}";

            _logger.LogInformation("Container name {@ContainerName}", containerName);

            var externalUrl = await _blobStorage.UploadImageAsync(stream, containerName, $"{hash}.png");

            _logger.LogInformation("External URL {@ExternalUrl}", externalUrl);

            // save a record for the hash and blob url
            await _documentStore.InsertOneAsync(collectionName, new DocumentBase()
            {
                Id = hash,
                Data = externalUrl.ToCleanUrl(),
                DocumentType = query.DocumentType,
                RoutingKey = _routingKeyGenerator.Generate(query.SourceDataProvider, externalUrl),
                Uri = externalUrl,
                SourceUrlHash = hash,
                SourceDataProvider = query.SourceDataProvider,
                Sport = query.Sport
            });

            _logger.LogInformation("Document saved to database");

            // return the url generated by our blob storage provider
            return Ok(new GetExternalImageResponse()
            {
                Id = hash,
                CanonicalId = query.CanonicalId,
                Uri = externalUrl
            });

        }

        [HttpPost("documentRequest", Name = "ProcessDocumentRequested")]
        public async Task<IActionResult> ProcessDocumentRequested([FromBody] DocumentRequested command)
        {
            await _bus.Publish(command);
            return Accepted(new { command.Uri, command.DocumentType, command.SourceDataProvider });
        }
    }
}
