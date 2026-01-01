namespace SportsData.Api.Application.UI.Messageboard.Dtos;

public sealed record PageResult<T>(IReadOnlyList<T> Items, string? NextCursor);
