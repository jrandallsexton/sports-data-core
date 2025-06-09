using System;

namespace SportsData.Core.Dtos.Canonical
{
    public record AthletePositionDto : DtoBase
    {
        public string Name { get; set; } = null!;

        public string DisplayName { get; set; } = null!;

        public string Abbreviation { get; set; } = null!;

        public bool Leaf { get; set; }

        public Guid? ParentId { get; set; }
    }
}
