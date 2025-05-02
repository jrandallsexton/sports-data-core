using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using Microsoft.Extensions.Logging;

namespace SportsData.Api.Application.Auth
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDataContext _dbContext;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AppDataContext dbContext, ILogger<AuthController> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        private string GetCookieDomain()
        {
            var host = Request.Host.Host;
            if (host.Contains("sportdeets.com"))
            {
                return ".sportdeets.com";
            }
            return "localhost";
        }

        [HttpGet]
        [Authorize]
        public IActionResult Get()
        {
            var claims = User.Claims
                .Select(c => new { c.Type, c.Value })
                .ToList();

            return Ok(new
            {
                Message = "You are authorized!",
                Claims = claims
            });
        }

        [HttpGet("claims")]
        [Authorize]
        public IActionResult GetMappedClaims([FromUser] FirebaseUserClaims userClaims)
        {
            return Ok(userClaims);
        }

        [HttpPost("set-token")]
        public IActionResult SetToken([FromBody] SetTokenRequest request)
        {
            if (string.IsNullOrEmpty(request.Token))
            {
                return BadRequest("Token is required");
            }

            _logger.LogInformation("Setting token cookie for request from {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);

            // Set the token as an HttpOnly cookie
            Response.Cookies.Append("authToken", request.Token, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTime.UtcNow.AddDays(7),
                Domain = GetCookieDomain()
            });

            return Ok(new { message = "Token set successfully" });
        }

        [HttpPost("clear-token")]
        public IActionResult ClearToken()
        {
            _logger.LogInformation("Clearing token cookie for request from {RemoteIpAddress}", HttpContext.Connection.RemoteIpAddress);

            // Clear the token cookie
            Response.Cookies.Delete("authToken", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Domain = GetCookieDomain()
            });

            return Ok(new { message = "Token cleared successfully" });
        }

        [HttpPost("auth-sync")]
        [Authorize]
        public async Task<IActionResult> SyncUser()
        {
            var userId = User.FindFirstValue("user_id");
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (userId == null || email == null)
                return Unauthorized();

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == userId);

            if (user == null)
            {
                user = new Infrastructure.Data.Entities.User
                {
                    FirebaseUid = userId,
                    Email = email,
                    CreatedUtc = DateTime.UtcNow
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { message = "User synced", userId = user.Id });
        }
    }

    public class SetTokenRequest
    {
        public string Token { get; set; }
    }
}
