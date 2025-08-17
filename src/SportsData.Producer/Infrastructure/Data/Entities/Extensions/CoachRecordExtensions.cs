using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CoachRecordExtensions
{
    public static CoachRecord AsEntity(
        this EspnCoachRecordDto dto,
        Guid coachId,
        IGenerateExternalRefIdentities identityGenerator,
        Guid correlationId)
    {
        if (dto.Ref is null)
            throw new ArgumentException("Coach record DTO is missing its $ref property.");

        var identity = identityGenerator.Generate(dto.Ref);

        var record = new CoachRecord
        {
            Id = identity.CanonicalId,
            CoachId = coachId,
            Name = dto.Name,
            Type = dto.Type,
            Summary = dto.Summary,
            DisplayValue = dto.DisplayValue,
            Value = dto.Value,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            ExternalIds =
            {
                new CoachRecordExternalId
                {
                    Id = Guid.NewGuid(),
                    CoachRecordId = identity.CanonicalId,
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
            var statEntity = new CoachRecordStat
            {
                Id = Guid.NewGuid(),
                CoachRecordId = record.Id,
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
