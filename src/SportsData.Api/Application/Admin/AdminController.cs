using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.Admin.Queries.AuditAi;
using SportsData.Api.Application.Contests.Commands.GenerateGameRecap;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Franchise;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Admin
{
    [ApiController]
    [Route("admin")]
    [AdminApiToken]
    public class AdminController : ApiControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            IProvideBackgroundJobs backgroundJobProvider,
            ILogger<AdminController> logger)
        {
            _backgroundJobProvider = backgroundJobProvider;
            _logger = logger;
        }

        /// <summary>
        /// Test game recap generation with large prompt + JSON data
        /// Example: POST /admin/ai/game-recap
        /// Body: { "gameDataJson": "{ ... your large JSON ... }", "reloadPrompt": false }
        /// </summary>
        [HttpPost]
        [Route("ai/game-recap")]
        public async Task<ActionResult<GameRecapResponse>> GenerateGameRecap(
            [FromBody] GenerateGameRecapCommand command,
            [FromServices] IGenerateGameRecapCommandHandler handler,
            CancellationToken cancellationToken)
        {
            var result = await handler.ExecuteAsync(command, cancellationToken);
            return result.ToActionResult();
        }

        /// <summary>
        /// Fan out ESPN sourcing for every FranchiseSeason in a season year,
        /// optionally narrowed to specific child document types. Producer is
        /// not publicly reachable; this proxy is the operator's entry point.
        ///
        /// Example: POST /admin/sourcing/franchise-seasons/FootballNcaa/2025
        /// Body: { "includeLinkedDocumentTypes": ["TeamSeasonRecord"] }
        /// (empty body object {} = the historical full cascade)
        /// </summary>
        [HttpPost]
        [Route("sourcing/franchise-seasons/{sport}/{seasonYear:int}")]
        public async Task<ActionResult<Guid>> RequestFranchiseSeasonSourcing(
            [FromRoute] Sport sport,
            [FromRoute] int seasonYear,
            [FromBody] FranchiseSeasonSourcingRequest request,
            [FromServices] IFranchiseClientFactory franchiseClientFactory,
            CancellationToken cancellationToken)
        {
            var client = franchiseClientFactory.Resolve(sport);
            var result = await client.RequestFranchiseSeasonSourcing(
                seasonYear, request, cancellationToken);
            return result.ToActionResult();
        }

        [HttpPost]
        [Route("ai-audit")]
        public IActionResult AiPreviewsAudit()
        {
            var correlationId = Guid.NewGuid();
            var query = new AuditAiQuery { CorrelationId = correlationId };
            _backgroundJobProvider.Enqueue<IAuditAiQueryHandler>(p => p.ExecuteAsync(query, CancellationToken.None));
            return Accepted(correlationId);
        }

    }
}
