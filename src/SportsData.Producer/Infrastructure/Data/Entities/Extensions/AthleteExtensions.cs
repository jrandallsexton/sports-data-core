using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

// TODO: Clean this up since I moved things from Athlete model
public static class AthleteExtensions
{
    public static Athlete AsEntity(
        this EspnAthleteDto dto,
        Guid athleteId,
        Guid? franchiseId,
        string sourceUrlHash,
        Guid correlationId)
    {
        return new Athlete()
        {
            Id = athleteId,
            Age = dto.Age,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            CurrentExperience = dto.Experience?.Years ?? 0,
            DisplayName = dto.DisplayName ?? string.Empty,
            DoB = DateTime.TryParse(dto.DateOfBirth, out var value) ? value : DateTime.MinValue,
            FirstName = dto.FirstName ?? string.Empty,
            //FranchiseId = franchiseId,
            HeightDisplay = dto.DisplayHeight ?? string.Empty,
            HeightIn = dto.Height,
            IsActive = dto.Active,
            LastName = dto.LastName ?? string.Empty,
            ShortName = dto.ShortName ?? string.Empty,
            WeightDisplay = dto.DisplayWeight ?? string.Empty,
            WeightLb = dto.Weight,
            ExternalIds =
            [
                new AthleteExternalId()
                {
                    Id = Guid.NewGuid(),
                    CreatedUtc = DateTime.UtcNow,
                    Provider = SourceDataProvider.Espn,
                    Value = dto.Id.ToString(),
                    SourceUrlHash = sourceUrlHash
                }
            ]
        };
    }

    public static AthleteDto ToCanonicalModel(this Athlete entity)
    {
        return new AthleteDto()
        {
            Id = entity.Id,
            CreatedUtc = entity.CreatedUtc,
            ShortName = entity.ShortName,
            DisplayName = entity.DisplayName,
            IsActive = entity.IsActive,
            FirstName = entity.FirstName,
            WeightLb = entity.WeightLb,
            WeightDisplay = entity.WeightDisplay,
            Age = entity.Age,
            CurrentExperience = entity.CurrentExperience,
            DoB = entity.DoB,
            HeightDisplay = entity.HeightDisplay,
            HeightIn = entity.HeightIn,
            LastName = entity.LastName,
            UpdatedUtc = entity.ModifiedUtc,
            //PositionId = entity.CurrentPosition,
            //PositionName = entity.Position.Name
        };
    }
}