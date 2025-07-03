using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CoachExtensions
{
    public static Coach AsEntity(this EspnCoachDto dto, Guid correlationId)
    {
        var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);
        return new Coach
        {
            Id = Guid.NewGuid(),
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
                    Value = sourceUrlHash,
                    SourceUrlHash = sourceUrlHash
                }
            }
        };
    }
}
