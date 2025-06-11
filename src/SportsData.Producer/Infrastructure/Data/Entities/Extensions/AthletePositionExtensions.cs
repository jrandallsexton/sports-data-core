using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthletePositionExtensions
{
    public static AthletePosition AsEntity(
        this EspnAthletePositionDto dto,
        Guid positionId,
        Guid? parentId = null)
    {
        return new AthletePosition
        {
            Id = positionId,
            Name = dto.Name,
            DisplayName = dto.DisplayName,
            Abbreviation = dto.Abbreviation,
            Leaf = dto.Leaf,
            ParentId = parentId,
            ExternalIds = [ new AthletePositionExternalId()
            {
                Id = Guid.NewGuid(),
                Value = dto.Id.ToString(),
                Provider = SourceDataProvider.Espn,
                UrlHash = HashProvider.GenerateHashFromUri(dto.Ref)
            }],
        };
    }

    public static AthletePositionDto AsCanonical(this AthletePosition entity)
    {
        return new AthletePositionDto
        {
            Id = entity.Id,
            Name = entity.Name,
            DisplayName = entity.DisplayName,
            Abbreviation = entity.Abbreviation,
            Leaf = entity.Leaf,
            ParentId = entity.ParentId
        };
    }
}