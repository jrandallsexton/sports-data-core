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
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid positionId,
        Guid? parentId = null)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new AthletePosition
        {
            Id = positionId,
            Name = dto.Name.ToCanonicalForm(),
            DisplayName = dto.DisplayName.ToCanonicalForm(),
            Abbreviation = dto.Abbreviation?.Trim().ToUpper() ?? string.Empty,
            Leaf = dto.Leaf,
            ParentId = parentId,
            ExternalIds = new List<AthletePositionExternalId>()
            {
                new()
                {
                    Id = identity.CanonicalId,
                    Value = identity.UrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            }
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