using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Messageboard.Commands.ToggleReaction;

public class ToggleReactionCommand
{
    public required Guid PostId { get; init; }

    public required Guid UserId { get; init; }

    public ReactionType? Type { get; init; }
}
