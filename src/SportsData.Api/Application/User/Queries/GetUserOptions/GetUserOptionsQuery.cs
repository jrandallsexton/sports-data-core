namespace SportsData.Api.Application.User.Queries.GetUserOptions;

public record GetUserOptionsQuery
{
    public Guid UserId { get; init; }
}
