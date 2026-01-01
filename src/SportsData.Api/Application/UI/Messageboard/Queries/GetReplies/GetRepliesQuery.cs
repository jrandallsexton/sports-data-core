namespace SportsData.Api.Application.UI.Messageboard.Queries.GetReplies;

public class GetRepliesQuery
{
    public required Guid ThreadId { get; init; }

    public Guid? ParentId { get; init; }

    public int Limit { get; init; } = 20;

    public string? Cursor { get; init; }
}
