using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionCompetitorExtensions
{
    public static CompetitionCompetitor AsEntity(
        this EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new CompetitionCompetitor
        {
            Id = identity.CanonicalId,
            CompetitionId = competitionId,
            FranchiseSeasonId = franchiseSeasonId,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = correlationId,
            Type = dto.Type,
            Order = dto.Order,
            HomeAway = dto.HomeAway,
            Winner = dto.Winner,
            CuratedRankCurrent = dto.EspnCuratedRank?.Current,
            ExternalIds =
            [
                new CompetitionCompetitorExternalId
                {
                    Id = Guid.NewGuid(),
                    Provider = SourceDataProvider.Espn,
                    Value = identity.UrlHash,
                    SourceUrl = dto.Ref.ToCleanUrl(),
                    SourceUrlHash = identity.UrlHash
                }
            ]
        };
    }
}