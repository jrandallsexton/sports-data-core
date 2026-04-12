using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Contracts
{
    public interface IHasExternalIds
    {
        IEnumerable<ExternalId> GetExternalIds();
    }

}