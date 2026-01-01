namespace SportsData.Api.Application.UI.Messageboard.Queries.GetThreads;

public class GetThreadsQuery
{
    public required Guid GroupId { get; init; }

    public int Limit { get; init; } = 20;

    public string? Cursor { get; init; }
}
