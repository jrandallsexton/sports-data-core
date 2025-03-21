﻿using MassTransit;

using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
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
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;
        private readonly IProvideBlobStorage _blobStorage;
        private readonly IProvideHashes _hashProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public DocumentController(
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder,
            ILogger<DocumentController> logger,
            IProvideBlobStorage blobStorage,
            IProvideHashes hashProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _documentService = documentService;
            _decoder = decoder;
            _logger = logger;
            _blobStorage = blobStorage;
            _hashProvider = hashProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        [HttpGet("{providerId}/{sportId}/{typeId}/{documentId}")]
        public async Task<IActionResult> GetDocument(
            SourceDataProvider providerId,
            Sport sportId,
            DocumentType typeId,
            long documentId)
        {
            var collectionName = _decoder.GetCollectionName(providerId, sportId, typeId, null);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(collectionName);

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, documentId);

            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();

            // TODO: Clean this up
            return dbItem != null ? Ok(dbItem.Data) : NotFound();
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

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(collectionName);

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, documentId);

            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();

            // TODO: Clean this up
            return dbItem != null ? Ok(dbItem.Data) : NotFound();
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
            var collectionName = _decoder.GetCollectionName(
                query.SourceDataProvider,
                query.Sport,
                query.DocumentType,
                query.SeasonYear);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(collectionName);

            // generate a hash for the collection retrieval
            var hash = _hashProvider.GenerateHashFromUrl(query.Url.ToLower());

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, hash);

            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();

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
                ? $"{query.Sport}{query.DocumentType.ToString()}{query.SeasonYear.Value}"
                : $"{query.Sport}{query.DocumentType.ToString()}";
            var externalUrl = await _blobStorage.UploadImageAsync(stream, containerName, $"{hash}.png");

            // save a record for the hash and blob url
            await dbObjects.InsertOneAsync(new DocumentBase()
            {
                Id = hash,
                CanonicalId = query.CanonicalId,
                Data = externalUrl
            });

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
