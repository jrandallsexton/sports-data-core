using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.Auth
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDataContext _dbContext;

        public AuthController(AppDataContext dbContext)
        {
            _dbContext = dbContext;
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

}
