using Microsoft.AspNetCore.Mvc;

namespace SportsData.Api.Application.UI.Conferences
{
    [ApiController]
    [Route("ui/conferences")]
    public class ConferenceController : ControllerBase
    {
        private readonly IConferenceService _conferenceService;

        public ConferenceController(IConferenceService conferenceService)
        {
            _conferenceService = conferenceService;
        }

        [HttpGet]
        public async Task<ActionResult<List<ConferenceNameAndSlugDto>>> GetConferenceNamesAndSlugs(CancellationToken cancellationToken)
        {
            var conferencesAndSlugs = await _conferenceService.GetConferenceNamesAndSlugs(cancellationToken);
            
            var result = conferencesAndSlugs
                .Select(item => new ConferenceNameAndSlugDto
                {
                    ShortName = item.ShortName,
                    Slug = item.Slug,
                    Division = item.Division
                })
                .ToList();

            return Ok(result);
        }
    }

    public class ConferenceNameAndSlugDto
    {
        public required string Division { get; set; }

        public required string ShortName { get; set; }

        public required string Slug { get; set; }
    }
}