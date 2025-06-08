using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Blobs;
using SportsData.Core.Infrastructure.Clients.Provider.Commands;
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

        public DocumentController(
            IDocumentStore documentStore,
            IDecodeDocumentProvidersAndTypes decoder,
            ILogger<DocumentController> logger,
            IProvideBlobStorage blobStorage,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _documentStore = documentStore;
            _decoder = decoder;
            _logger = logger;
            _blobStorage = blobStorage;
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpGet("urlHash/{hash}")]
        public async Task<IActionResult> GetDocumentByUrlHash(string hash)
        {
            using (_logger.BeginScope(new Dictionary<string, object> { ["UrlHash"] = hash }))
            {
                _logger.LogInformation("Started");

                var collectionName = "FootballNcaa";

                _logger.LogInformation("Collection name decoded {@CollectionName}", collectionName);

                var dbItem = await _documentStore
                    .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.UrlHash == hash);

                _logger.LogInformation(dbItem == null ? "No document found" : "Document found");

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
                _logger.LogInformation("Started");

                var collectionName = _decoder.GetCollectionName(providerId, sportId, typeId, null);

                _logger.LogInformation("Collection name decoded {@CollectionName}", collectionName);

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
        public async Task<IActionResult> PublishDocumentEvents([FromBody]PublishDocumentEventsCommand command)
        {
            _backgroundJobProvider.Enqueue<PublishDocumentEventsProcessor>(x => x.Process(command));
            return Accepted();
        }

        /// <summary>
        /// Gets the blob storage url for an image, or fetches it, uploads to blob, and returns the canonical url
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        [HttpPost("external", Name = "GetExternalDocument")]
        public async Task<ActionResult<GetExternalDocumentQueryResponse>> GetExternalDocument([FromBody] GetExternalDocumentQuery query)
        {
            _logger.LogInformation("Began with {@Query}", query);

            var collectionName = _decoder.GetCollectionName(
                query.SourceDataProvider,
                query.Sport,
                query.DocumentType,
                query.SeasonYear);

            _logger.LogInformation("Collection name decoded {@CollectionName}", collectionName);

            // generate a hash for the collection retrieval
            var hash = HashProvider.GenerateHashFromUrl(query.Url.ToLower());

            _logger.LogInformation("Hash generated {@Hash}", hash);

            // Look for the item in the database first to see if we already have a link to it in blob storage
            var dbItem = await _documentStore
                .GetFirstOrDefaultAsync<DocumentBase>(collectionName, x => x.id == hash.ToString());

            if (dbItem is not null)
            {
                return Ok(new GetExternalDocumentQueryResponse()
                {
                    Id = hash,
                    CanonicalId = query.CanonicalId,
                    Href = dbItem.Data
                });
            }

            // get the item via the url
            using var client = new HttpClient();
            using var response = await client.GetAsync(query.Url);
            await using var stream = await response.Content.ReadAsStreamAsync();

            // upload it to blob storage
            var containerName = query.SeasonYear.HasValue
                ? $"provider-{query.Sport.ToString().ToLower()}-{query.DocumentType.ToString().ToLower()}-{query.SeasonYear.Value}"
                : $"provider-{query.Sport.ToString().ToLower()}-{query.DocumentType.ToString().ToLower()}";

            _logger.LogInformation("Container name {@ContainerName}", containerName);

            var externalUrl = await _blobStorage.UploadImageAsync(stream, containerName, $"{hash}.png");

            _logger.LogInformation("External URL {@ExternalUrl}", externalUrl);

            // save a record for the hash and blob url
            await _documentStore.InsertOneAsync(collectionName, new DocumentBase()
            {
                Id = hash,
                Data = externalUrl
            });

            _logger.LogInformation("Document saved to database");

            // return the url generated by our blob storage provider
            return Ok(new GetExternalDocumentQueryResponse()
            {
                Id = hash,
                CanonicalId = query.CanonicalId,
                Href = externalUrl
            });
        }
    }
}
