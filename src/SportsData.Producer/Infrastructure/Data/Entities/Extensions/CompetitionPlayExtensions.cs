using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionPlayExtensions
{
    public static FootballCompetitionPlay AsFootballEntity(
        this EspnFootballEventCompetitionPlayDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId,
        Guid competitionId,
        Guid? driveId,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new FootballCompetitionPlay
        {
            Id = identity.CanonicalId,
            EspnId = dto.Id,
            SequenceNumber = dto.SequenceNumber,
            Text = dto.Text ?? "UNK",
            TypeId = dto.Type?.Id ?? "9999",
            DriveId = driveId,
            ClockDisplayValue = dto.Clock?.DisplayValue,
            ClockValue = dto.Clock?.Value ?? 0,
            EndDistance = dto.End?.Distance,
            EndDown = dto.End?.Down,
            EndFranchiseSeasonId = endFranchiseSeasonId,
            EndYardLine = dto.End?.YardLine,
            EndYardsToEndzone = dto.End?.YardsToEndzone,
            StartDistance = dto.Start?.Distance,
            StartDown = dto.Start?.Down,
            StartYardLine = dto.Start?.YardLine,
            StartYardsToEndzone = dto.Start?.YardsToEndzone,
            StatYardage = dto.StatYardage
        };

        MapSharedProperties(dto, entity, identity, correlationId, competitionId, startFranchiseSeasonId);
        return entity;
    }

    public static BaseballCompetitionPlay AsBaseballEntity(
        this EspnBaseballEventCompetitionPlayDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId,
        Guid competitionId,
        Guid? teamFranchiseSeasonId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new BaseballCompetitionPlay
        {
            Id = identity.CanonicalId,
            EspnId = dto.Id,
            SequenceNumber = dto.SequenceNumber,
            Text = dto.Text ?? "UNK",
            TypeId = dto.Type?.Id ?? "9999"
        };

        MapSharedProperties(dto, entity, identity, correlationId, competitionId, teamFranchiseSeasonId);
        return entity;
    }

    private static void MapSharedProperties(
        EspnEventCompetitionPlayDtoBase dto,
        CompetitionPlayBase entity,
        ExternalRefIdentity identity,
        Guid correlationId,
        Guid competitionId,
        Guid? startFranchiseSeasonId)
    {
        entity.AlternativeText = dto.AlternativeText;
        entity.AwayScore = dto.AwayScore;
        entity.CreatedBy = correlationId;
        entity.CreatedUtc = DateTime.UtcNow;
        entity.CompetitionId = competitionId;
        entity.HomeScore = dto.HomeScore;
        entity.Modified = dto.Modified;
        entity.PeriodNumber = dto.Period?.Number ?? 0;
        entity.Priority = dto.Priority;
        entity.ScoreValue = dto.ScoreValue;
        entity.ScoringPlay = dto.ScoringPlay;
        entity.ShortAlternativeText = dto.ShortAlternativeText;
        entity.ShortText = dto.ShortText;
        entity.StartFranchiseSeasonId = startFranchiseSeasonId;
        entity.Type = dto.Type?.Id is not null && Enum.TryParse<PlayType>(dto.Type.Id, out var parsedType) ? parsedType : PlayType.Unknown;
        entity.ExternalIds = new List<CompetitionPlayExternalId>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Value = identity.UrlHash,
                Provider = SourceDataProvider.Espn,
                SourceUrlHash = identity.UrlHash,
                SourceUrl = identity.CleanUrl
            }
        };
    }
}
