using System;

namespace SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts
{
    public interface IHasRef
    {
        Uri Ref { get; }
    }
}
