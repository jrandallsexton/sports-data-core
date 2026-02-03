using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteSeasonInjuryExtensions
{
    /// <summary>
    /// Converts an ESPN TeamSeasonInjury DTO to an AthleteSeasonInjury entity.
    /// </summary>
    public static AthleteSeasonInjury AsEntity(
        this EspnTeamSeasonInjuryDto dto,
        ExternalRefIdentity identity,
        Guid athleteSeasonId,
        Guid correlationId)
    {
        var headline = dto.GetHeadlineText();
        var text = dto.GetBodyText();

        return new AthleteSeasonInjury
        {
            Id = identity.CanonicalId,
            AthleteSeasonId = athleteSeasonId,
            TypeId = dto.Type?.Id ?? string.Empty,
            Type = dto.GetTypeName(),
            TypeDescription = dto.Type?.Description,
            TypeAbbreviation = dto.Type?.Abbreviation,
            Date = dto.Date,
            Headline = headline,
            Text = text,
            Source = dto.GetSourceName(),
            Status = dto.Status,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = correlationId
        };
    }
}
