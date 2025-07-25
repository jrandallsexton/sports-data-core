using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class PlayExtensions
{
    public static Play AsEntity(
        this EspnEventCompetitionPlayDto dto,
        Guid competitionId,
        Guid? driveId,
        Guid teamFranchiseSeasonId,
        Guid? startTeamFranchiseSeasonId = null)
    {
        return new Play
        {
            Id = Guid.NewGuid(),
            EspnId = dto.Id,
            SequenceNumber = dto.SequenceNumber,
            TypeId = dto.Type.Id,
            Text = dto.Text,
            ShortText = dto.ShortText,
            AlternativeText = dto.AlternativeText,
            ShortAlternativeText = dto.ShortAlternativeText,
            AwayScore = dto.AwayScore,
            HomeScore = dto.HomeScore,
            PeriodNumber = dto.Period?.Number ?? 0,
            ClockValue = dto.Clock?.Value ?? 0,
            ClockDisplayValue = dto.Clock?.DisplayValue,
            ScoringPlay = dto.ScoringPlay,
            Priority = dto.Priority,
            ScoreValue = dto.ScoreValue,
            Modified = dto.Modified,
            TeamFranchiseSeasonId = teamFranchiseSeasonId,
            StartDown = dto.Start?.Down,
            StartDistance = dto.Start?.Distance,
            StartYardLine = dto.Start?.YardLine,
            StartYardsToEndzone = dto.Start?.YardsToEndzone,
            StartTeamFranchiseSeasonId = startTeamFranchiseSeasonId,
            EndDown = dto.End?.Down,
            EndDistance = dto.End?.Distance,
            EndYardLine = dto.End?.YardLine,
            EndYardsToEndzone = dto.End?.YardsToEndzone,
            StatYardage = dto.StatYardage,
            DriveId = driveId,
            CompetitionId = competitionId,
            Type = Enum.Parse<PlayType>(dto.Type.Id)
        };
    }
}
