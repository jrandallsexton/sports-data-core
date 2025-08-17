using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class PlayExtensions
{
    public static Play AsEntity(
        this EspnEventCompetitionPlayDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId,
        Guid competitionId,
        Guid? driveId,
        Guid teamFranchiseSeasonId,
        Guid? startTeamFranchiseSeasonId = null)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Play
        {
            Id = identity.CanonicalId,
            AlternativeText = dto.AlternativeText,
            AwayScore = dto.AwayScore,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            ClockDisplayValue = dto.Clock?.DisplayValue,
            ClockValue = dto.Clock?.Value ?? 0,
            CompetitionId = competitionId,
            DriveId = driveId,
            EndDistance = dto.End?.Distance,
            EndDown = dto.End?.Down,
            EndYardLine = dto.End?.YardLine,
            EndYardsToEndzone = dto.End?.YardsToEndzone,
            EspnId = dto.Id,
            HomeScore = dto.HomeScore,
            Modified = dto.Modified,
            PeriodNumber = dto.Period?.Number ?? 0,
            Priority = dto.Priority,
            ScoreValue = dto.ScoreValue,
            ScoringPlay = dto.ScoringPlay,
            SequenceNumber = dto.SequenceNumber,
            ShortAlternativeText = dto.ShortAlternativeText,
            ShortText = dto.ShortText,
            StartDistance = dto.Start?.Distance,
            StartDown = dto.Start?.Down,
            StartTeamFranchiseSeasonId = startTeamFranchiseSeasonId,
            StartYardLine = dto.Start?.YardLine,
            StartYardsToEndzone = dto.Start?.YardsToEndzone,
            StatYardage = dto.StatYardage,
            TeamFranchiseSeasonId = teamFranchiseSeasonId,
            Text = dto.Text ?? "UNK", // TODO: This popped up as null in some data; need to investigate
            Type = dto.Type?.Id is null ? PlayType.Unknown: Enum.Parse<PlayType>(dto.Type.Id),
            TypeId = dto.Type?.Id is null ? "9999" : dto.Type.Id,
            ExternalIds = new List<PlayExternalId>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Value = identity.UrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            }
        };
    }
}
