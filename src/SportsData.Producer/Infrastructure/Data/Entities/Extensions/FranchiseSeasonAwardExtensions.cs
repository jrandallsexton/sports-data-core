using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonAwardExtensions
    {
        /// <summary>
        /// Converts an ESPN Award DTO to an Award entity using pre-computed canonical identity.
        /// Awards require special URL canonicalization (removing /seasons/YYYY segment) before
        /// identity generation, so the identity is passed in rather than computed here.
        /// </summary>
        public static Award AsEntity(
            this EspnAwardDto dto,
            ExternalRefIdentity identity,
            Guid correlationId)
        {
            return new Award
            {
                Id = identity.CanonicalId,
                Name = dto.Name,
                Description = dto.Description,
                History = dto.History,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ModifiedUtc = DateTime.UtcNow,
                ModifiedBy = correlationId,
                ExternalIds = new List<AwardExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        AwardId = identity.CanonicalId,
                        Provider = SourceDataProvider.Espn,
                        Value = identity.UrlHash,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = correlationId,
                        ModifiedUtc = DateTime.UtcNow,
                        ModifiedBy = correlationId
                    }
                }
            };
        }
    }
}
