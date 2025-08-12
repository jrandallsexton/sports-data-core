using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class SeasonExtensions
    {
        public static Season AsEntity(
            this EspnFootballSeasonDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Season DTO is missing its $ref property.");

            // Generate canonical ID and hash from the season ref
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var season = new Season
            {
                Id = identity.CanonicalId,
                Year = dto.Year,
                Name = dto.DisplayName,
                StartDate = DateTime.Parse(dto.StartDate).ToUniversalTime(),
                EndDate = DateTime.Parse(dto.EndDate).ToUniversalTime(),
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds =
                {
                    new SeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };

            return season;
        }
    }
}
