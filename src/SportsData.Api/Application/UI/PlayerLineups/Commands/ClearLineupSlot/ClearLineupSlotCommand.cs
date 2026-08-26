namespace SportsData.Api.Application.UI.PlayerLineups.Commands.ClearLineupSlot;

public record ClearLineupSlotCommand(
    Guid LeagueId,
    Guid UserId,
    int SeasonYear,
    int SeasonWeek,
    string SlotId);
