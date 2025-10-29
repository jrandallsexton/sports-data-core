using Microsoft.AspNetCore.Mvc;

using SportsData.Core.Common;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Competitions
{
    [Route("api/competition")]
    [ApiController]
    public class CompetitionController : ControllerBase
    {
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly ICompetitionService _competitionService;

        public CompetitionController(
            IProvideBackgroundJobs backgroundJobProvider,
            ICompetitionService competitionService)
        {
            _backgroundJobProvider = backgroundJobProvider;
            _competitionService = competitionService;
        }

        [HttpPost]
        [Route("{competitionId}/metrics/generate")]
        public IActionResult GenerateMetrics([FromRoute] Guid competitionId)
        {
            _backgroundJobProvider.Enqueue<ICompetitionMetricService>(p =>
                p.CalculateCompetitionMetrics(competitionId));

            return Accepted(new { Message = $"Competition {competitionId} metric generation initiated." });
        }

        [HttpPost]
        [Route("metrics/generate")]
        public IActionResult RefreshCompetitionMetrics()
        {
            _backgroundJobProvider.Enqueue<ICompetitionService>(p => p.RefreshCompetitionMetrics());

            return Accepted(new { Message = $"Competition metric generation initiated." });
        }

        [HttpPost]
        [Route("{competitionId}/drives/refresh")]
        public async Task<IActionResult> RefreshDrives([FromRoute] Guid competitionId)
        {
            var result = await _competitionService.RefreshCompetitionDrives(competitionId);

            if (result.IsSuccess)
            {
                return Accepted(new { Message = $"Competition {competitionId} drives refresh initiated." });
            }

            // TODO: refactor common failure handling
            if (result is Failure<Guid> failure)
            {
                return result.Status switch
                {
                    ResultStatus.Validation => BadRequest(new { failure.Errors }),
                    ResultStatus.NotFound => NotFound(new { failure.Errors }),
                    ResultStatus.Unauthorized => Unauthorized(new { failure.Errors }),
                    ResultStatus.Forbid => Forbid(),
                    _ => StatusCode(500, new { failure.Errors })
                };
            }

            return StatusCode(500); // fallback safety
        }

        [HttpPost]
        [Route("{competitionId}/media/refresh")]
        public Task<IActionResult> RefreshMedia([FromRoute] Guid competitionId)
        {
            _backgroundJobProvider.Enqueue<ICompetitionService>(p => p.RefreshCompetitionMedia(competitionId));

            return Task.FromResult<IActionResult>(
                Accepted(new { Message = $"Competition media generation initiated." }));
        }

        [HttpPost]
        [Route("media/refresh")]
        public Task<IActionResult> RefreshMedia()
        {
            // TODO: Remove hard-coding
            _backgroundJobProvider.Enqueue<ICompetitionService>(p => p.RefreshCompetitionMedia(2025));

            return Task.FromResult<IActionResult>(
                Accepted(new { Message = $"Competition media generation initiated." }));
        }
    }
}
