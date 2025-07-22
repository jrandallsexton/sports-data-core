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

        var coachIdentity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Coach
        {
            Id = coachIdentity.CanonicalId,
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            Experience = dto.Experience,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ExternalIds = new List<CoachExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = coachIdentity.UrlHash,
                    SourceUrlHash = coachIdentity.UrlHash,
                    SourceUrl = coachIdentity.CleanUrl
                }
            }
        };
    }
}
