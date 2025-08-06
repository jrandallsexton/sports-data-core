using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SportsData.Api.Application.User;
using SportsData.Api.Extensions;

using System.Security.Claims;

[ApiController]
[Route("[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserService userService, ILogger<UserController> logger)
    {
        _userService = userService;
        _logger = logger;
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> UpsertUser([FromBody] UpsertUserCommand dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var firebaseUid = User.FindFirst("user_id")?.Value;
        if (firebaseUid == null)
            return Unauthorized();

        var provider = User.FindFirstValue("sign_in_provider") ?? "unknown";

        var updated = await _userService.GetOrCreateUserAsync(
            firebaseUid,
            dto.Email,
            dto.DisplayName,
            photoUrl: null, // you could include this in `UpsertUserCommand`
            signInProvider: provider,
            emailVerified: false // or wire it through too
        );

        return Ok();
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = HttpContext.GetCurrentUserId();
        return Ok(await _userService.GetUserDtoById(userId));
    }
}