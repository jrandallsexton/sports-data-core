using System;

namespace SportsData.Core.Dtos.Canonical
{
    public abstract class DtoBase
    {
        public Guid Id { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }
    }
}
