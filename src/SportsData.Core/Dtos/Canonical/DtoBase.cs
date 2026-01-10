using System;

namespace SportsData.Core.Dtos.Canonical
{
    public abstract record DtoBase
    {
        public Guid Id { get; init; }

        public Uri? Ref { get; set; }

        public DateTime CreatedUtc { get; init; }

        public DateTime? UpdatedUtc { get; init; }
    }
}
