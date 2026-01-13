using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Contest.Commands.RefreshContestMedia;

public class RefreshContestMediaCommand
{
    public required Guid ContestId { get; init; }
    public required Sport Sport { get; init; }
}
