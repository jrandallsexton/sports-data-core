using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Application.User.Commands.UpsertUser;

public class UpsertUserCommand
{
    [Required]
    public required string Email { get; init; }

    public string? DisplayName { get; init; }
}
