using System;

namespace SportsData.Core.Models.Canonical
{
    public abstract class DtoBase
    {
        public Guid Id { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }
    }
}
