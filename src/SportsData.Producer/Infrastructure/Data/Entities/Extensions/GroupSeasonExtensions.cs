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
            Guid groupId,
            Guid groupSeasonId,
            int seasonYear,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new GroupSeason
            {
                Id = identity.CanonicalId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                Season = seasonYear,
                GroupId = groupId,
                Name = dto.Name,
                Abbreviation = dto.Abbreviation,
                ShortName = dto.ShortName,
                MidsizeName = dto.MidsizeName,
                Slug = dto.Slug,
                ExternalIds = new List<GroupSeasonExternalId>()
                {
                    new GroupSeasonExternalId()
                    {
                        Id = Guid.NewGuid(),
                        Value = dto.Id.ToString(),
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };
        }


        public static ConferenceSeasonDto ToCanonicalModel(this GroupSeason entity)
        {
            return new ConferenceSeasonDto()
            {
                // TODO: Implement
            };
        }
    }
}
