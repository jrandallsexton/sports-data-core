using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionCompetitorScoreExtensions
{
    public static CompetitionCompetitorScore AsEntity(
        this EspnEventCompetitionCompetitorScoreDto dto,
        Guid competitionCompetitorId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        SourceDataProvider provider,
        Guid correlationId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new CompetitionCompetitorScore
        {
            Id = identity.CanonicalId,
            CompetitionCompetitorId = competitionCompetitorId,
            Value = dto.Value,
            DisplayValue = dto.DisplayValue,
            Winner = dto.Winner,
            SourceId = dto.Source?.Id ?? string.Empty,
            SourceDescription = dto.Source?.Description ?? string.Empty,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            ExternalIds =
            [
                new CompetitionCompetitorScoreExternalId
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