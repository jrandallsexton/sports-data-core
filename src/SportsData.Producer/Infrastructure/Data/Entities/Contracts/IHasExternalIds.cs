using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Contracts
{
    public interface IHasExternalIds
    {
        IEnumerable<ExternalId> GetExternalIds();
    }

}