using System;

namespace SportsData.Core.Dtos.Canonical
{
    public class AthletePositionDto : DtoBase
    {
        public string Name { get; set; } = string.Empty;

        public string DisplayName { get; set; } = string.Empty;

        public string Abbreviation { get; set; } = string.Empty;

        public bool Leaf { get; set; }

        public Guid? ParentId { get; set; }
    }
}
