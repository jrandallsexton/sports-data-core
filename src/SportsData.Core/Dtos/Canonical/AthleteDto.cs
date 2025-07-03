using System;

namespace SportsData.Core.Dtos.Canonical
{
    public record AthleteDto : DtoBase
    {
        public string LastName { get; init; } = null!;

        public string FirstName { get; init; } = null!;

        public string? DisplayName { get; init; }

        public string? ShortName { get; init; }

        public decimal? WeightLb { get; init; }

        public string? WeightDisplay { get; init; }

        public decimal? HeightIn { get; init; }

        public string? HeightDisplay { get; init; }

        public int Age { get; init; } = -1; // default to -1 for unknown age

        public DateTime? DoB { get; init; }

        public int CurrentExperience { get; init; } = -1; // default to -1 for unknown experience

        public bool IsActive { get; init; }

        public Guid PositionId { get; init; }

        public string? PositionName { get; init; }
    }
}
