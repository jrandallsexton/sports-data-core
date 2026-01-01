namespace SportsData.Api.Application.UI.Messageboard.Queries.GetThreadsByUserGroups;

public class GetThreadsByUserGroupsQuery
{
    public required Guid UserId { get; init; }

    public int PerGroupLimit { get; init; } = 5;
}
