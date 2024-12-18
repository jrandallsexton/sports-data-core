using System.Collections.Generic;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware.Health
{
    public interface IProvideHealthChecks
    {
        string GetProviderName();
        Task<Dictionary<string, object>> GetHealthStatus();
    }
}