namespace SportsData.Api.Application.User.Commands.DeleteAccount;

public class DeleteAccountCommand
{
    /// <summary>The authenticated user to delete (resolved from the JWT).</summary>
    public required Guid UserId { get; init; }
}
