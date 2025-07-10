using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonAwardExtensions
    {
        public static (Award, AwardExternalId, FranchiseSeasonAward) AsEntity(
            this EspnAwardDto dto,
            Guid franchiseSeasonId,
            Guid correlationId,
            string espnAwardId,
            string espnAwardSourceUrlHash)
        {
            var award = new Award
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                History = dto.History,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow
            };

            var awardExternalId = new AwardExternalId
            {
                Id = Guid.NewGuid(),
                Award = award,
                Provider = SourceDataProvider.Espn,
                Value = espnAwardId,
                SourceUrlHash = espnAwardSourceUrlHash,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                SourceUrl = dto.Ref.ToCleanUrl()
            };
            award.ExternalIds.Add(awardExternalId);

            var seasonAward = new FranchiseSeasonAward
            {
                Id = Guid.NewGuid(),
                FranchiseSeasonId = franchiseSeasonId,
                Award = award,
                AwardId = award.Id,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                Winners = dto.Winners?.Select(w => new FranchiseSeasonAwardWinner
                {
                    Id = Guid.NewGuid(),
                    AthleteRef = w.Athlete?.Ref?.ToString(),
                    TeamRef = w.Team?.Ref?.ToString(),
                    CreatedBy = correlationId,
                    CreatedUtc = DateTime.UtcNow
                }).ToList() ?? new()
            };

            return (award, awardExternalId, seasonAward);
        }
    }
}
