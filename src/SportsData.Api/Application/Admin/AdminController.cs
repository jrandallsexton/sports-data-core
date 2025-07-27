using Microsoft.AspNetCore.Mvc;
using SportsData.Core.Common.Hashing;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public AdminController(IGenerateExternalRefIdentities externalRefIdentityGenerator)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
        }

        [HttpPost]
        [Route("generate-url-identity")]
        public IActionResult GenerateUrlIdentity([FromBody] GenerateUrlIdentityCommand command)
        {
            if (string.IsNullOrWhiteSpace(command.Url))
            {
                return BadRequest("URL cannot be empty.");
            }
            // Here you would typically generate a unique identity based on the URL.
            // For simplicity, we will just return the URL as is.
            var identity = _externalRefIdentityGenerator.Generate(command.Url);

            return Ok(identity);
        }
    }

    public class GenerateUrlIdentityCommand
    {
        public string Url { get; set; } = string.Empty;
    }
}
