using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class PositionExtensions
    {
        public static Position AsEntity(
            this EspnAthletePositionDto dto,
            Guid positionId,
            Guid correlationId)
        {
            return new Position()
            {
                Id = positionId,
                Abbrevation = dto.Abbreviation,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                DisplayName = dto.DisplayName,
                ExternalIds = [new PositionExternalId() { Id = Guid.NewGuid(), Value = dto.Id.ToString(), Provider = SourceDataProvider.Espn }],
                IsLeaf = dto.Leaf,
                Name = dto.Name
            };
        }

        public static PositionDto ToCanonicalModel(this Position entity)
        {
            return new PositionDto()
            {
                Id = entity.Id,
                Abbrevation = entity.Abbrevation,
                CreatedUtc = entity.CreatedUtc,
                DisplayName = entity.DisplayName,
                Name = entity.Name
            };
        }
    }
}
