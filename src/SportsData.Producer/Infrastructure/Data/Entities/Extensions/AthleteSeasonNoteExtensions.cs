using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class AthleteSeasonNoteExtensions
{
    /// <summary>
    /// Converts an ESPN AthleteSeasonNote DTO to an AthleteSeasonNote entity.
    /// </summary>
    public static AthleteSeasonNote AsEntity(
        this EspnAthleteSeasonNoteDto dto,
        ExternalRefIdentity identity,
        Guid athleteSeasonId,
        Guid correlationId)
    {
        var headline = dto.GetHeadlineText();
        var text = dto.GetBodyText();

        return new AthleteSeasonNote
        {
            Id = identity.CanonicalId,
            AthleteSeasonId = athleteSeasonId,
            Type = dto.GetTypeName(),
            Date = dto.Date,
            Headline = headline,
            Text = text,
            Source = dto.Source,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ModifiedUtc = DateTime.UtcNow,
            ModifiedBy = correlationId
        };
    }
}
