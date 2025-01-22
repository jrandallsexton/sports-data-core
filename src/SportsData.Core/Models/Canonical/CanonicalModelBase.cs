using System;

namespace SportsData.Core.Models.Canonical
{
    public class CanonicalModelBase
    {
        public Guid Id { get; set; }

        public DateTime CreatedUtc { get; set; }

        public DateTime? UpdatedUtc { get; set; }
    }
}
