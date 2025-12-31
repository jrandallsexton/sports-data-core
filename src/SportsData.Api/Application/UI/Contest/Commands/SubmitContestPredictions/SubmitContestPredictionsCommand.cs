using SportsData.Api.Application.UI.Contest.Dtos;

namespace SportsData.Api.Application.UI.Contest.Commands.SubmitContestPredictions;

public class SubmitContestPredictionsCommand
{
    public required Guid UserId { get; init; }

    public required List<ContestPredictionDto> Predictions { get; init; }
}
