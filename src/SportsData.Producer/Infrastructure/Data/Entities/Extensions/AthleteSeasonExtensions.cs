using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteSeasonExtensions
{
    public static AthleteSeason AsEntity(
        this EspnAthleteSeasonDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid franchiseSeasonId,
        Guid positionId,
        Guid athleteId,
        Guid correlationId)
    {
        if (dto.Ref is null)
            throw new ArgumentException("Missing $ref on EspnAthleteSeasonDto");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new AthleteSeason
        {
            Id = identity.CanonicalId,
            AthleteId = athleteId,
            FranchiseSeasonId = franchiseSeasonId,
            PositionId = positionId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DisplayName = dto.DisplayName,
            ShortName = dto.ShortName,
            Slug = dto.Slug,
            HeightIn = (decimal)dto.Height,
            HeightDisplay = dto.DisplayHeight,
            WeightLb = (decimal)dto.Weight,
            WeightDisplay = dto.DisplayWeight,
            Jersey = dto.Jersey,
            IsActive = dto.Active,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ExternalIds =
            [
                new AthleteSeasonExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = identity.UrlHash,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash
                }
            ]
        };
    }
}