namespace SportsData.Api.Application.UI.Messageboard.Commands.CreateReply;

public class CreateReplyCommand
{
    public required Guid ThreadId { get; init; }

    public Guid? ParentPostId { get; init; }

    public required Guid UserId { get; init; }

    public required string Content { get; init; }
}
