using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Application.User
{
    public class UserDto
    {
        public string? FirebaseUid { get; set; }

        [Required]
        public string Email { get; set; } = null!;

        public string? DisplayName { get; set; }

        public string? PhotoUrl { get; set; }

        public string? Timezone { get; set; }
    }

}
