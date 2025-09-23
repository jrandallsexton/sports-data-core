using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class EspnEventCompetitionPredictorDtoExtensions
    {
        public static List<CompetitionPrediction> AsEntities(
            this EspnEventCompetitionPredictorDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid homeFranchiseSeasonId,
            Guid awayFranchiseSeasonId,
            Guid correlationId,
            Dictionary<string, PredictionMetric> knownMetrics)
        {
            var result = new List<CompetitionPrediction>();

            if (dto.HomeTeam?.Statistics is not null)
            {
                var homePrediction = CreatePrediction(
                    dto.HomeTeam.Statistics,
                    competitionId,
                    homeFranchiseSeasonId,
                    isHome: true,
                    correlationId,
                    knownMetrics);

                result.Add(homePrediction);
            }

            if (dto.AwayTeam?.Statistics is not null)
            {
                var awayPrediction = CreatePrediction(
                    dto.AwayTeam.Statistics,
                    competitionId,
                    awayFranchiseSeasonId,
                    isHome: false,
                    correlationId,
                    knownMetrics);

                result.Add(awayPrediction);
            }

            return result;
        }

        private static CompetitionPrediction CreatePrediction(
            List<EspnEventCompetitionPredictorTeamStatistic> stats,
            Guid competitionId,
            Guid franchiseSeasonId,
            bool isHome,
            Guid correlationId,
            Dictionary<string, PredictionMetric> knownMetrics)
        {
            var prediction = new CompetitionPrediction
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                FranchiseSeasonId = franchiseSeasonId,
                IsHome = isHome,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
            };

            foreach (var stat in stats)
            {
                var key = stat.Name.Trim().ToLowerInvariant();

                if (!knownMetrics.TryGetValue(key, out var metric))
                {
                    metric = new PredictionMetric
                    {
                        Id = Guid.NewGuid(),
                        Name = stat.Name,
                        DisplayName = stat.DisplayName,
                        ShortDisplayName = stat.ShortDisplayName,
                        Abbreviation = stat.Abbreviation,
                        Description = stat.Description
                    };

                    knownMetrics[key] = metric;
                }

                var value = new CompetitionPredictionValue
                {
                    Id = Guid.NewGuid(),
                    CompetitionPredictionId = prediction.Id,
                    PredictionMetricId = metric.Id,
                    Value = Convert.ToDecimal(stat.Value),
                    DisplayValue = stat.DisplayValue,
                    CreatedBy = correlationId,
                    CreatedUtc = DateTime.UtcNow
                };

                prediction.Values.Add(value); // ✅ Add the value to the nav prop
            }

            return prediction;
        }

    }
}
