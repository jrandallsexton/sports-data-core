using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Contest;

public class ContestPredictionDto
{
    public Guid ContestId { get; set; }

    public Guid WinnerFranchiseSeasonId { get; set; }

    public decimal WinProbability { get; set; }

    public PickType PredictionType { get; set; }

    public required string ModelVersion { get; set; }
}

public static class ContestPredictionExtensions
{
    public static ContestPrediction AsEntity(this ContestPredictionDto dto)
    {
        return new ContestPrediction
        {
            ContestId = dto.ContestId,
            WinnerFranchiseSeasonId = dto.WinnerFranchiseSeasonId,
            WinProbability = dto.WinProbability,
            PredictionType = dto.PredictionType,
            ModelVersion = dto.ModelVersion
        };
    }
}