using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using System.Security.Claims;

namespace SportsData.Api.Application.User
{
    [ApiController]
    [Route("[controller]")]
    public class UserController : ApiControllerBase
    {
        private readonly AppDataContext _dbContext;

        public UserController(AppDataContext dbContext)
        {
            _dbContext = dbContext;
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> UpsertUser([FromBody] UserDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var firebaseUid = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (firebaseUid == null)
                return Unauthorized();

            var user = await _dbContext.Users
                .FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

            if (user == null)
            {
                user = new Infrastructure.Data.Entities.User
                {
                    FirebaseUid = firebaseUid,
                    Email = dto.Email,
                    DisplayName = dto.DisplayName,
                    Timezone = dto.Timezone,
                    LastLoginUtc = DateTime.UtcNow
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
    }
}
