using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Consumers.Receivers
{
    public interface IEventReceiver
    {
        Task<List<EventingBase>> Receive();
        Task Delete(Guid eventId);
    }
}
