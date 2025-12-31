namespace SportsData.Api.Application.UI.Messageboard.Dtos;

public sealed record PageRequest(int Limit = 20, string? Cursor = null);
