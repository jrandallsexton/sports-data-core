using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionCompetitorLineScoreExtensions
{
    public static CompetitionCompetitorLineScore AsEntity(
        this EspnEventCompetitionCompetitorLineScoreDto dto,
        Guid competitionCompetitorId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        SourceDataProvider provider,
        Guid correlationId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new CompetitionCompetitorLineScore
        {
            Id = identity.CanonicalId,
            CompetitionCompetitorId = competitionCompetitorId,
            Value = dto.Value,
            DisplayValue = dto.DisplayValue,
            Period = dto.Period,
            SourceId = dto.Source?.Id ?? string.Empty,
            SourceDescription = dto.Source?.Description ?? string.Empty,
            SourceState = dto.Source?.State,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ExternalIds =
            [
                new CompetitionCompetitorLineScoreExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = provider,
                    Value = identity.UrlHash,
                    SourceUrl = identity.CleanUrl,
                    SourceUrlHash = identity.UrlHash
                }
            ]
        };
    }
}