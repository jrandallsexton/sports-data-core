﻿using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class DriveExtensions
{
    public static Drive AsEntity(
        this EspnEventCompetitionDriveDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid contestCompetitionId,
        Guid? startFranchiseSeasonId = null,
        Guid? endFranchiseSeasonId = null)
    {
        if (dto.Ref == null)
            throw new ArgumentException($"{nameof(EspnEventCompetitionDriveDto)} is missing its $ref property.");

        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        return new Drive
        {
            Id = identity.CanonicalId,
            ContestCompetitionId = contestCompetitionId,
            Description = dto.Description,
            DisplayResult = dto.DisplayResult,
            EndClockDisplayValue = dto.End?.Clock?.DisplayValue,
            EndClockValue = dto.End?.Clock?.Value,
            EndDistance = dto.End?.Distance,
            EndDown = dto.End?.Down,
            EndDownDistanceText = dto.End?.DownDistanceText,
            EndFranchiseSeasonId = endFranchiseSeasonId,
            EndPeriodNumber = dto.End?.Period?.Number,
            EndPeriodType = dto.End?.Period?.Type,
            EndShortDownDistanceText = dto.End?.ShortDownDistanceText,
            EndText = dto.End?.Text,
            EndYardLine = dto.End?.YardLine,
            EndYardsToEndzone = dto.End?.YardsToEndzone,
            IsScore = dto.IsScore,
            OffensivePlays = dto.OffensivePlays,
            Ordinal = int.TryParse(dto.SequenceNumber, out var ordinal) ? ordinal : 0,
            Result = dto.Result,
            SequenceNumber = dto.SequenceNumber,
            ShortDisplayResult = dto.ShortDisplayResult,
            SourceDescription = dto.Source?.Description,
            SourceId = dto.Source?.Id,
            StartClockDisplayValue = dto.Start?.Clock?.DisplayValue,
            StartClockValue = dto.Start?.Clock?.Value,
            StartDistance = dto.Start?.Distance,
            StartDown = dto.Start?.Down,
            StartDownDistanceText = dto.Start?.DownDistanceText,
            StartFranchiseSeasonId = startFranchiseSeasonId,
            StartPeriodNumber = dto.Start?.Period?.Number,
            StartPeriodType = dto.Start?.Period?.Type,
            StartShortDownDistanceText = dto.Start?.ShortDownDistanceText,
            StartText = dto.Start?.Text,
            StartYardLine = dto.Start?.YardLine,
            StartYardsToEndzone = dto.Start?.YardsToEndzone,
            TimeElapsedDisplay = dto.TimeElapsed?.DisplayValue,
            TimeElapsedValue = dto.TimeElapsed?.Value,
            Yards = dto.Yards
        };
    }
}
