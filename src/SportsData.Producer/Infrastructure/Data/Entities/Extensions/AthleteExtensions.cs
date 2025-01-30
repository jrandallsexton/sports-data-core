using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class AthleteExtensions
    {
        public static Athlete AsEntity(
            this EspnAthleteDto dto,
            Guid athleteId,
            Guid? franchiseId,
            Guid correlationId)
        {
            return new Athlete()
            {
                Id = athleteId,
                Age = dto.Age,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                CurrentExperience = dto.Experience?.Years ?? 0,
                DisplayName = dto.DisplayName,
                DoB = DateTime.Parse(dto.DateOfBirth),
                FirstName = dto.FirstName,
                FranchiseId = franchiseId,
                HeightDisplay = dto.DisplayHeight,
                HeightIn = dto.Height,
                IsActive = dto.Active,
                LastName = dto.LastName,
                ShortName = dto.ShortName,
                WeightDisplay = dto.DisplayWeight,
                WeightLb = dto.Weight
            };
        }

        public static AthleteCanonicalModel ToCanonicalModel(this Athlete entity)
        {
            return new AthleteCanonicalModel()
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
                PositionId = entity.CurrentPosition,
                PositionName = entity.Position.Name
            };
        }
    }
}
