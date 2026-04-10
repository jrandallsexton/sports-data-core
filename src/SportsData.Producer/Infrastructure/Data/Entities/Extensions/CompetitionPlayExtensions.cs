using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionPlayExtensions
{
    public static CompetitionPlay AsEntity(
        this EspnEventCompetitionPlayDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid correlationId,
        Guid competitionId,
        Guid? driveId,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId
        )
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new CompetitionPlay
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
            EndFranchiseSeasonId = endFranchiseSeasonId,
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
            StartFranchiseSeasonId = startFranchiseSeasonId,
            StartYardLine = dto.Start?.YardLine,
            StartYardsToEndzone = dto.Start?.YardsToEndzone,
            StatYardage = dto.StatYardage,
            Text = dto.Text ?? "UNK", // This popped up as null in some data; default to "UNK"
            Type = dto.Type?.Id is not null && Enum.TryParse<PlayType>(dto.Type.Id, out var parsedType) ? parsedType : PlayType.Unknown,
            TypeId = dto.Type?.Id is null ? "9999" : dto.Type.Id,
            ExternalIds = new List<CompetitionPlayExternalId>
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
