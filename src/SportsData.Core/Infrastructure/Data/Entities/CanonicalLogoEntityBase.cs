using System.Collections.Generic;

namespace SportsData.Core.Infrastructure.Data.Entities;

public abstract class CanonicalLogoEntityBase<T> : CanonicalEntityBase<T>
{
    public string Url { get; set; }

    public long? Height { get; set; }

    public long? Width { get; set; }

    public List<string>? Rel { get; set; }
}