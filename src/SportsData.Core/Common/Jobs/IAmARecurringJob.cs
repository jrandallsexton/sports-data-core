using System.Threading.Tasks;

namespace SportsData.Core.Common.Jobs
{ public interface IAmARecurringJob
    {
        Task ExecuteAsync();
    }
}
