using SportsData.Core.Common;

namespace SportsData.Api.Application.Contests.Commands.ReenrichContest;

public class ReenrichContestCommand
{
    public required Guid ContestId { get; init; }
    public required Sport Sport { get; init; }
}
