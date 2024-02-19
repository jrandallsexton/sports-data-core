using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Publishers
{
    public interface IEventPublisher
    {
        Task PublishAsync(CancellationToken cancellationToken);
    }
}
