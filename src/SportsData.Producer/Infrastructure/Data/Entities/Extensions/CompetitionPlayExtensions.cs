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
            StatYardage = dto.StatYardage,
            Wallclock = dto.Wallclock == default ? null : dto.Wallclock,
            ScoringTypeName = dto.ScoringType?.Name,
            ScoringTypeDisplayName = dto.ScoringType?.DisplayName,
            ScoringTypeAbbreviation = dto.ScoringType?.Abbreviation,
            PointAfterAttemptId = dto.PointAfterAttempt?.Id,
            PointAfterAttemptText = dto.PointAfterAttempt?.Text,
            PointAfterAttemptAbbreviation = dto.PointAfterAttempt?.Abbreviation,
            PointAfterAttemptValue = dto.PointAfterAttempt?.Value
        };

        MapSharedProperties(dto, entity, identity, correlationId, competitionId, startFranchiseSeasonId);
        return entity;
    }

    public static BaseballCompetitionPlay AsBaseballEntity(
        this EspnBaseballEventCompetitionPlayDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId,
        Guid competitionId,
        Guid? teamFranchiseSeasonId,
        Guid? atBatAthleteSeasonId,
        Guid? pitchingAthleteSeasonId)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new BaseballCompetitionPlay
        {
            Id = identity.CanonicalId,
            EspnId = dto.Id,
            SequenceNumber = dto.SequenceNumber,
            Text = dto.Text ?? "UNK",
            TypeId = dto.Type?.Id ?? "9999",
            HalfInning = dto.Period?.Type,
            Outs = dto.Outs,
            Wallclock = dto.Wallclock == default ? null : dto.Wallclock,
            IsValid = dto.Valid,
            AtBatId = dto.AtBatId,
            AtBatPitchNumber = dto.AtBatPitchNumber,
            BatOrder = dto.BatOrder,
            BatsType = dto.Bats?.Type,
            BatsAbbreviation = dto.Bats?.Abbreviation,
            PitchesType = dto.Pitches?.Type,
            PitchesAbbreviation = dto.Pitches?.Abbreviation,
            AtBatAthleteSeasonId = atBatAthleteSeasonId,
            PitchingAthleteSeasonId = pitchingAthleteSeasonId,
            PitchCoordinateX = dto.PitchCoordinate?.X,
            PitchCoordinateY = dto.PitchCoordinate?.Y,
            HitCoordinateX = dto.HitCoordinate?.X,
            HitCoordinateY = dto.HitCoordinate?.Y,
            PitchTypeId = dto.PitchType?.Id,
            PitchTypeText = dto.PitchType?.Text,
            PitchTypeAbbreviation = dto.PitchType?.Abbreviation,
            PitchVelocity = dto.PitchVelocity,
            PitchCountBalls = dto.PitchCount?.Balls,
            PitchCountStrikes = dto.PitchCount?.Strikes,
            ResultCountBalls = dto.ResultCount?.Balls,
            ResultCountStrikes = dto.ResultCount?.Strikes,
            Trajectory = dto.Trajectory,
            StrikeType = dto.StrikeType,
            SummaryType = dto.SummaryType,
            AwayHits = dto.AwayHits,
            HomeHits = dto.HomeHits,
            AwayErrors = dto.AwayErrors,
            HomeErrors = dto.HomeErrors,
            RbiCount = dto.RbiCount,
            IsDoublePlay = dto.DoublePlay,
            IsTriplePlay = dto.TriplePlay
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
