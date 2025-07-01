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
            Guid groupId,
            Guid correlationId)
        {
            var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);
            return new Group()
            {
                Id = groupId,
                Abbreviation = dto.Abbreviation,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds =
                [
                    new GroupExternalId()
                    {
                        Id = Guid.NewGuid(),
                        Value = sourceUrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = sourceUrlHash
                    }
                ],
                IsConference = dto.IsConference,
                MidsizeName = dto.MidsizeName,
                Name = dto.Name,
                //ParentGroupId = espnDto.Parent. // TODO: Determine how to set/get this
                ShortName = dto.ShortName,
                Seasons = [],
                Slug = SlugGenerator.GenerateSlug([dto.ShortName, dto.Name])
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
