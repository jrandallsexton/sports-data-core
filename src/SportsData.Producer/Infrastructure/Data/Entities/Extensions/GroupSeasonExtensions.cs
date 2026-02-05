using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class GroupSeasonExtensions
    {
        public static GroupSeason AsEntity(
            this EspnGroupSeasonDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            int seasonYear,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new GroupSeason
            {
                Id = identity.CanonicalId,
                Abbreviation = dto.Abbreviation ?? "UNK",
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                IsConference = dto.IsConference,
                MidsizeName = dto.MidsizeName,
                Name = dto.Name,
                SeasonYear = seasonYear,
                ShortName = dto.ShortName,
                Slug = dto.Slug,
                ExternalIds = new List<GroupSeasonExternalId>()
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash,
                        CreatedBy = correlationId,
                        CreatedUtc = DateTime.UtcNow
                    }
                }
            };
        }


        public static ConferenceSeasonDto ToCanonicalModel(this GroupSeason entity)
        {
            // Note: ConferenceSeasonDto is currently an empty record in Core.
            // When properties are added to the DTO, map from entity here.
            return new ConferenceSeasonDto
            {
                Id = entity.Id,
                CreatedUtc = entity.CreatedUtc
            };
        }
    }
}
