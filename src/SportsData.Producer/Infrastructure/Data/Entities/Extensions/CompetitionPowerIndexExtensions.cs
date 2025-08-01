using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionPowerIndexExtensions
    {
        public static CompetitionPowerIndex AsEntity(
            this EspnEventCompetitionPowerIndexStat dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Uri parentRef,
            Guid powerIndexId,
            Guid competitionId,
            Guid franchiseSeasonId,
            Guid correlationId)
        {
            if (parentRef is null)
                throw new ArgumentException("PowerIndex DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(parentRef);
            var competitionPowerIndexId = Guid.NewGuid();
            return new CompetitionPowerIndex
            {
                Id = competitionPowerIndexId,
                CompetitionId = competitionId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                DisplayValue = dto.DisplayValue,
                FranchiseSeasonId = franchiseSeasonId,
                PowerIndexId = powerIndexId,
                Value = dto.Value,
                ExternalIds =
                [
                    new CompetitionPowerIndexExternalId
                    {
                        Id = Guid.NewGuid(),
                        CompetitionPowerIndexId = competitionPowerIndexId,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash,
                        Value = identity.UrlHash,
                    }
                ]
            };
        }
    }
}
