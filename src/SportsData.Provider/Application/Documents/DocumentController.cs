using Microsoft.AspNetCore.Mvc;

using MongoDB.Driver;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn.DTOs.Venue;

namespace SportsData.Provider.Application.Documents
{
    [Route("api/document")]
    public class DocumentController : ApiControllerBase
    {

        private readonly IMongoCollection<Venue> _venues;

        public DocumentController(DataService dataService)
        {
            _venues = dataService.Database?.GetCollection<Venue>("venues");
        }

        [HttpGet("{type}/{documentId}")]
        public async Task<IActionResult> GetDocument(DocumentType type, int documentId)
        {
            var filter = Builders<Venue>.Filter.Eq(x => x.Id, documentId);
            var dbVenueResult = await _venues.FindAsync(filter);
            var dbVenue = await dbVenueResult.FirstOrDefaultAsync();
            return await Task.FromResult(Ok(dbVenue.ToJson()));
        }
    }
}
