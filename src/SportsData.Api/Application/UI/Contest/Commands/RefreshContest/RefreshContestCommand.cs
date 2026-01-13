using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Contest.Commands.RefreshContest;

public class RefreshContestCommand
{
    public required Guid ContestId { get; init; }
    public required Sport Sport { get; init; }
}
