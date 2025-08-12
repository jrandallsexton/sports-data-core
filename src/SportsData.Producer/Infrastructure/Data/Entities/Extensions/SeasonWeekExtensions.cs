using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class SeasonWeekExtensions
    {
        public static SeasonWeek AsEntity(
            this EspnFootballSeasonTypeWeekDto dto,
            Guid seasonId,
            Guid seasonPhaseId,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("SeasonTypeWeek DTO is missing its $ref property.");

            // Generate canonical ID and hash from the season type week ref
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);
            var seasonWeek = new SeasonWeek
            {
                Id = identity.CanonicalId,
                SeasonId = seasonId,
                SeasonPhaseId = seasonPhaseId,
                Number = dto.Number,
                StartDate = DateTime.Parse(dto.StartDate).ToUniversalTime(),
                EndDate = DateTime.Parse(dto.EndDate).ToUniversalTime(),
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds =
                {
                    new SeasonWeekExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };
            return seasonWeek;
        }
    }
}
