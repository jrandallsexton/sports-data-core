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
            _logger.LogInformation("Determining cookie domain for host: {Host}", host);

            // Check if we're in Azure (contains azurewebsites.net)
            if (host.Contains("azurewebsites.net"))
            {
                _logger.LogInformation("Using domain: .sportdeets.com for Azure environment");
                return ".sportdeets.com";
            }

            // For local development
            _logger.LogInformation("Using domain: localhost for local development");
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
            try
            {
                var domain = GetCookieDomain();
                var remoteIp = Request.HttpContext.Connection.RemoteIpAddress;
                _logger.LogInformation("Setting token cookie for request from {RemoteIp}. Domain: {Domain}, Origin: {Origin}", 
                    remoteIp, domain, Request.Headers["Origin"].ToString());

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Domain = domain,
                    Path = "/"
                };

                _logger.LogInformation("Cookie set with options: HttpOnly={HttpOnly}, Secure={Secure}, SameSite={SameSite}, Domain={Domain}, Path={Path}",
                    cookieOptions.HttpOnly, cookieOptions.Secure, cookieOptions.SameSite, cookieOptions.Domain, cookieOptions.Path);

                Response.Cookies.Append("authToken", request.Token, cookieOptions);

                var setCookieHeader = Response.Headers["Set-Cookie"].ToString();
                _logger.LogInformation("Set-Cookie header: {SetCookieHeader}", setCookieHeader);

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting token cookie");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("clear-token")]
        public IActionResult ClearToken()
        {
            try
            {
                var domain = GetCookieDomain();
                var remoteIp = Request.HttpContext.Connection.RemoteIpAddress;
                _logger.LogInformation("Clearing token cookie for request from {RemoteIp}. Domain: {Domain}, Origin: {Origin}", 
                    remoteIp, domain, Request.Headers["Origin"].ToString());

                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.None,
                    Domain = domain,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddDays(-1)
                };

                _logger.LogInformation("Cookie cleared with options: HttpOnly={HttpOnly}, Secure={Secure}, SameSite={SameSite}, Domain={Domain}, Path={Path}",
                    cookieOptions.HttpOnly, cookieOptions.Secure, cookieOptions.SameSite, cookieOptions.Domain, cookieOptions.Path);

                Response.Cookies.Delete("authToken", cookieOptions);
                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing token cookie");
                return StatusCode(500, "Internal server error");
            }
        }

        [HttpPost("auth-sync")]
        [Authorize]
        public async Task<IActionResult> SyncUser()
        {
            var userId = User.FindFirstValue("user_id");
            var email = User.FindFirstValue(ClaimTypes.Email);
            var provider = User.FindFirstValue("sign_in_provider") ?? "unknown";

            if (userId == null || email == null)
                return Unauthorized();

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.FirebaseUid == userId);

            if (user == null)
            {
                user = new Infrastructure.Data.Entities.User
                {
                    Id = Guid.NewGuid(),
                    FirebaseUid = userId,
                    Email = email,
                    CreatedUtc = DateTime.UtcNow,
                    SignInProvider = provider,
                    LastLoginUtc = DateTime.UtcNow,
                };

                _dbContext.Users.Add(user);
                await _dbContext.SaveChangesAsync();
            }

            return Ok(new { message = "User synced", userId = user.Id });
        }
    }

    public class SetTokenRequest
    {
        public string Token { get; set; } = null!;
    }
}
