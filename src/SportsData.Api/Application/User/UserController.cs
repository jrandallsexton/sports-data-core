using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Auth;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using System.Security.Claims;

namespace SportsData.Api.Application.User
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDataContext _dbContext;

        public UserController(AppDataContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpsertUser([FromBody] UpsertUserCommand dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var firebaseUid = User.FindFirst("user_id")?.Value;
            if (firebaseUid == null)
                return Unauthorized();

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

            var provider = User.FindFirstValue("sign_in_provider") ?? "unknown";

            if (user == null)
            {
                user = new Infrastructure.Data.Entities.User
                {
                    Id = Guid.NewGuid(),
                    FirebaseUid = firebaseUid,
                    Email = dto.Email,
                    DisplayName = dto.DisplayName,
                    Timezone = dto.Timezone,
                    LastLoginUtc = DateTime.UtcNow,
                    SignInProvider = provider
                };
                _dbContext.Users.Add(user);
            }
            else
            {
                user.DisplayName = dto.DisplayName;
                user.Timezone = dto.Timezone;
                user.LastLoginUtc = DateTime.UtcNow;
            }

            await _dbContext.SaveChangesAsync();
            return Ok();
        }

        [HttpGet("me")]
        [Authorize]
        public async Task<ActionResult<UserDto>> GetCurrentUser()
        {
            var firebaseUid = User.FindFirst("user_id")?.Value;
            if (firebaseUid == null)
                return Unauthorized();

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

            if (user == null)
                return NotFound(new { message = $"User with UID '{firebaseUid}' not found." });

            var dto = new UserDto
            {
                FirebaseUid = user.FirebaseUid,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Timezone = user.Timezone,
                LastLoginUtc = user.LastLoginUtc
                //PhotoUrl = user.PhotoUrl
            };

            return Ok(dto);
        }

    }
}
