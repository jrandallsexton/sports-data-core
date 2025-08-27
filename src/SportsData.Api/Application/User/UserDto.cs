﻿using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Application.User
{
    public class UserDto
    {
        public Guid Id { get; set; }

        public string? FirebaseUid { get; set; }

        [Required]
        public string Email { get; set; } = null!;

        public string? DisplayName { get; set; }

        public string? PhotoUrl { get; set; }

        public string? Timezone { get; set; }

        public DateTime LastLoginUtc { get; set; }

        public IList<UserLeagueMembership> Leagues { get; set; } = [];

        public bool IsAdmin { get; set; } = false;

        public class UserLeagueMembership
        {
            public Guid Id { get; set; }

            public required string Name { get; set; }
        }
    }
}
