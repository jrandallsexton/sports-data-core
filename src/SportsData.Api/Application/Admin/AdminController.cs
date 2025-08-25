using Microsoft.AspNetCore.Mvc;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Clients.AI;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IProvideAiCommunication _ai;

        public AdminController(
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IProvideAiCommunication ai)
        {
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _ai = ai;
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

        [HttpPost]
        [Route("ai-test")]
        public async Task<IActionResult> TestAiCommunications([FromBody] AiChatCommand command)
        {
            return Ok(await _ai.GetResponseAsync(command.Text));
        }
    }

    public class GenerateUrlIdentityCommand
    {
        public string Url { get; set; } = string.Empty;
    }

    public class AiChatCommand
    {
        public required string Name { get; set; }
        public required string Text { get; set; }
    }
}
