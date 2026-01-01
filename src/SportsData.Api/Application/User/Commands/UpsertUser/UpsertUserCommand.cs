namespace SportsData.Api.Application.User.Commands.UpsertUser;

public class UpsertUserCommand
{
    public string Email { get; init; } = string.Empty;

    public string? DisplayName { get; init; }
}
