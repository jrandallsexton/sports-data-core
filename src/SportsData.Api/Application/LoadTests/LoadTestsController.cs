using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.LoadTests.Commands.GenerateLoadTest;
using SportsData.Api.Infrastructure.Auth;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

namespace SportsData.Api.Application.LoadTests;

/// <summary>
/// KEDA/Hangfire load tests: POST starts one. Admin-only: [AdminApiToken] is
/// on the class.
/// </summary>
[ApiController]
[Route("api/load-tests")]
[AdminApiToken]
public class LoadTestsController : ApiControllerBase
{
    /// <summary>
    /// Generates synthetic load to test KEDA autoscaling.
    /// Publishes events to RabbitMQ which are consumed and enqueued to Hangfire.
    /// KEDA monitors Hangfire queue depth and autoscales pods accordingly.
    /// </summary>
    /// <param name="command">Load test configuration</param>
    /// <param name="handler">Command handler</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test execution details</returns>
    [HttpPost]
    public async Task<ActionResult<GenerateLoadTestResult>> GenerateLoadTest(
        [FromBody] GenerateLoadTestCommand command,
        [FromServices] IGenerateLoadTestCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.ExecuteAsync(command, cancellationToken);
        return result.ToActionResult();
    }
}
