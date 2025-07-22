using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class ContestPlayExtensions
    {
        public static Play AsEntity(
            this EspnEventCompetitionDriveItemPlayDto dto,
            Guid contestPlayId,
            Guid contestCompetitionId,
            Guid contestDriveId,
            Guid teamFranchiseSeasonId,
            Guid correlationId)
        {
            return new Play
            {
                Id = contestPlayId,
                CompetitionId = contestCompetitionId,
                DriveId = contestDriveId,
                EspnId = dto.Id,
                SequenceNumber = dto.SequenceNumber,
                TypeId = dto.Type?.Id ?? string.Empty,
                TypeText = dto.Type?.Text ?? string.Empty,
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
                // StartTeamFranchiseSeasonId should be resolved separately if needed
                EndDown = dto.End?.Down,
                EndDistance = dto.End?.Distance,
                EndYardLine = dto.End?.YardLine,
                EndYardsToEndzone = dto.End?.YardsToEndzone,
                // EndTeamFranchiseSeasonId should be resolved separately if needed
                StatYardage = dto.StatYardage,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId
            };
        }
    }
}
