using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Consumers
{
    public interface IEventConsumer
    {
        Task ConsumeAsync(CancellationToken cancellationToken);
    }
}
