using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthletePositionExtensions
{
    public static AthletePosition AsEntity(
        this EspnAthletePositionDto dto,
        Guid positionId,
        Guid? parentId = null)
    {
        var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);
        return new AthletePosition
        {
            Id = positionId,
            Name = dto.Name.ToCanonicalForm(),
            DisplayName = dto.DisplayName.ToCanonicalForm(),
            Abbreviation = dto.Abbreviation?.Trim().ToUpper() ?? string.Empty,
            Leaf = dto.Leaf,
            ParentId = parentId,
            ExternalIds = [ new AthletePositionExternalId()
            {
                Id = Guid.NewGuid(),
                Value = sourceUrlHash,
                Provider = SourceDataProvider.Espn,
                SourceUrlHash = sourceUrlHash,
                SourceUrl = dto.Ref.ToCleanUrl()
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