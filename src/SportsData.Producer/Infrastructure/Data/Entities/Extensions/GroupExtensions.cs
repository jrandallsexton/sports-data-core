using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;
using SportsData.Producer.Application.Slugs;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class GroupExtensions
    {
        public static Group AsEntity(
            this EspnGroupBySeasonDto dto,
            Guid groupId,
            Guid correlationId,
            ISlugGenerator slugGenerator)
        {
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
                        Id = Guid.NewGuid(), Value = dto.Id.ToString(),
                        Provider = SourceDataProvider.Espn
                    }
                ],
                IsConference = dto.IsConference,
                MidsizeName = dto.MidsizeName,
                Name = dto.Name,
                //ParentGroupId = espnDto.Parent. // TODO: Determine how to set/get this
                ShortName = dto.ShortName,
                Seasons = [],
                Slug = slugGenerator.GenerateSlug([dto.ShortName, dto.Name])
            };
        }

        public static ConferenceCanonicalModel ToCanonicalModel(this Group entity)
        {
            return new ConferenceCanonicalModel()
            {
                // TODO: Imeplement
            };
        }
    }
}
