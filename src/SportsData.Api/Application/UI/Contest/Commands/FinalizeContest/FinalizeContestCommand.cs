using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Contest.Commands.FinalizeContest;

public class FinalizeContestCommand
{
    public required Guid ContestId { get; init; }
    public required Sport Sport { get; init; }
}
