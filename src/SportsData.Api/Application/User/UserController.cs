using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.User.Commands.UpsertUser;
using SportsData.Api.Application.User.Dtos;
using SportsData.Api.Application.User.Queries.GetMe;
using SportsData.Api.Extensions;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

using System.Security.Claims;

namespace SportsData.Api.Application.User;

[ApiController]
[Route("[controller]")]
public class UserController : ApiControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<Guid>> UpsertUser(
        [FromBody] UpsertUserCommand command,
        [FromServices] IUpsertUserCommandHandler handler,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var firebaseUid = User.FindFirst("user_id")?.Value;
        if (firebaseUid == null)
            return Unauthorized();

        var provider = User.FindFirstValue("sign_in_provider") ?? "unknown";

        var result = await handler.ExecuteAsync(command, firebaseUid, provider, cancellationToken);

        return result.ToActionResult();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserDto>> GetMe(
        [FromServices] IGetMeQueryHandler handler,
        CancellationToken cancellationToken)
    {
        var userId = HttpContext.GetCurrentUserId();

        var query = new GetMeQuery { UserId = userId };
        var result = await handler.ExecuteAsync(query, cancellationToken);

        return result.ToActionResult();
    }
}
