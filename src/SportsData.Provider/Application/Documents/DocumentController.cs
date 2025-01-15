using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Application.Documents
{
    [Route("api/document")]
    public class DocumentController : ApiControllerBase
    {
        private readonly DocumentService _documentService;
        private readonly IDecodeDocumentProvidersAndTypes _decoder;

        public DocumentController(
            DocumentService documentService,
            IDecodeDocumentProvidersAndTypes decoder)
        {
            _documentService = documentService;
            _decoder = decoder;
        }

        [HttpGet("{providerId}/{typeId}/{documentId}")]
        public async Task<IActionResult> GetDocument(
            SourceDataProvider providerId,
            DocumentType typeId,
            int documentId)
        {
            var type = _decoder.GetType(providerId, typeId);

            var dbObjects = _documentService.Database.GetCollection<DocumentBase>(type.Name);

            var filter = Builders<DocumentBase>.Filter.Eq(x => x.Id, documentId);

            var dbResult = await dbObjects.FindAsync(filter);
            var dbItem = await dbResult.FirstOrDefaultAsync();
            return await Task.FromResult(Ok(dbItem.Data));
        }
    }
}
