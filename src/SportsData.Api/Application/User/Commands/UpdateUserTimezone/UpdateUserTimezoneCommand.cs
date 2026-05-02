namespace SportsData.Api.Application.User.Commands.UpdateUserTimezone;

public class UpdateUserTimezoneCommand
{
    // null/empty clears the user's saved timezone (falls back to ET in the UI).
    public string? Timezone { get; init; }
}
