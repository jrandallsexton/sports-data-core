namespace SportsData.Api.Application.UI.Messageboard.Commands.CreateThread;

public class CreateThreadCommand
{
    public required Guid GroupId { get; init; }

    public required Guid UserId { get; init; }

    public string? Title { get; init; }

    public required string Content { get; init; }
}
