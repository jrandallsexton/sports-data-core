using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Publishers.Broadcasters
{
    public interface IEventBroadcaster
    {
        Task Broadcast<T>(T outgoingEvent) where T : EventingBase;
    }
}
