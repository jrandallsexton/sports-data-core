using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Slugs;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class GroupExtensions
    {
        public static Group AsEntity(
            this EspnGroupBySeasonDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid groupId,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new Group()
            {
                Id = groupId,
                Abbreviation = dto.Abbreviation,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                IsConference = dto.IsConference,
                MidsizeName = dto.MidsizeName,
                Name = dto.Name,
                //ParentGroupId = espnDto.Parent. // TODO: Determine how to set/get this
                ShortName = dto.ShortName,
                Seasons = [],
                Slug = SlugGenerator.GenerateSlug([dto.ShortName, dto.Name]),
                ExternalIds =
                [
                    new GroupExternalId()
                    {
                        Id = identity.CanonicalId,
                        Value = identity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl
                    }
                ]
            };
        }

        public static ConferenceDto ToCanonicalModel(this Group entity)
        {
            return new ConferenceDto()
            {
                // TODO: Implement
            };
        }
    }
}
