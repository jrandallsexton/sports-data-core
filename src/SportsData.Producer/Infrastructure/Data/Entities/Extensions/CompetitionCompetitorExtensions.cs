using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

// Sport-specific projections from EspnEventCompetitionCompetitorDto.
// CuratedRankCurrent is football-only (NCAA poll snapshot); MLB has no
// analogue. See docs/competition-competitor-split.md.
public static class CompetitionCompetitorExtensions
{
    public static BaseballCompetitionCompetitor AsBaseballEntity(
        this EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new BaseballCompetitionCompetitor
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
            ExternalIds =
            [
                BuildExternalId(identity, dto)
            ]
        };

        return entity;
    }

    public static FootballCompetitionCompetitor AsFootballEntity(
        this EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new FootballCompetitionCompetitor
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
                BuildExternalId(identity, dto)
            ]
        };

        return entity;
    }

    private static CompetitionCompetitorExternalId BuildExternalId(
        ExternalRefIdentity identity,
        EspnEventCompetitionCompetitorDto dto)
    {
        return new CompetitionCompetitorExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            Value = identity.UrlHash,
            SourceUrl = dto.Ref.ToCleanUrl(),
            SourceUrlHash = identity.UrlHash
        };
    }
}
