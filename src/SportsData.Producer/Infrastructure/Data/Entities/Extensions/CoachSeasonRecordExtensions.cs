using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CoachSeasonRecordExtensions
{
    public static CoachSeasonRecord AsEntity(
        this EspnCoachSeasonRecordDto dto,
        Guid coachSeasonId,
        IGenerateExternalRefIdentities identityGenerator,
        Guid correlationId)
    {
        if (dto.Ref is null)
            throw new ArgumentException("Coach season record DTO is missing its $ref property.");

        var identity = identityGenerator.Generate(dto.Ref);

        var record = new CoachSeasonRecord
        {
            Id = identity.CanonicalId,
            CoachSeasonId = coachSeasonId,
            Name = dto.Name,
            Type = dto.Type,
            Summary = dto.Summary,
            DisplayValue = dto.DisplayValue,
            Value = dto.Value,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            ExternalIds =
            {
                new CoachSeasonRecordExternalId
                {
                    Id = Guid.NewGuid(),
                    CoachSeasonRecordId = identity.CanonicalId,
                    Value = identity.UrlHash,
                    SourceUrl = identity.CleanUrl,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash
                }
            }
        };

        if (dto.Stats is null)
            return record;

        foreach (var stat in dto.Stats)
        {
            var statEntity = new CoachSeasonRecordStat
            {
                Id = Guid.NewGuid(),
                CoachSeasonRecordId = record.Id,
                Name = stat.Name,
                DisplayName = stat.DisplayName,
                ShortDisplayName = stat.ShortDisplayName,
                Description = stat.Description,
                Abbreviation = stat.Abbreviation,
                Type = stat.Type,
                Value = stat.Value,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow
            };

            record.Stats.Add(statEntity);
        }

        return record;
    }
}
