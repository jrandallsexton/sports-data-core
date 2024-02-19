using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Providers
{
    public interface IEventDataProvider : IDisposable
    {
        DbSet<OutgoingEvent> OutgoingEvents { get; set; }
        DbSet<IncomingEvent> IncomingEvents { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
