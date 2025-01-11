using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Application.Documents
{
    [Route("api/document")]
    public class DocumentController : ApiControllerBase
    {

        private readonly IMongoCollection<EspnVenueDto> _venues;

        public DocumentController(DocumentService dataService)
        {
            _venues = dataService.Database?.GetCollection<EspnVenueDto>(nameof(EspnVenueDto));
        }

        [HttpGet("{type}/{documentId}")]
        public async Task<IActionResult> GetDocument(DocumentType type, int documentId)
        {
            var filter = Builders<EspnVenueDto>.Filter.Eq(x => x.Id, documentId);
            var dbVenueResult = await _venues.FindAsync(filter);
            var dbVenue = await dbVenueResult.FirstOrDefaultAsync();
            return await Task.FromResult(Ok(dbVenue.ToJson()));
        }
    }
}
