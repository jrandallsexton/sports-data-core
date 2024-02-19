using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Consumers
{
    public interface IEventHandlerProvider
    {
        Dictionary<string, Func<string, Task>> GetEventHandlers();
    }
}
