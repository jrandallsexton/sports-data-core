using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CoachExtensions
{
    public static Coach AsEntity(
        this EspnCoachDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        if (dto.Ref == null)
            throw new ArgumentException("Coach DTO is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Coach
        {
            Id = identity.CanonicalId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DateOfBirth = dto.DateOfBirth,
            Experience = dto.Experience,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ExternalIds = new List<CoachExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = identity.UrlHash,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            }
        };
    }
}
