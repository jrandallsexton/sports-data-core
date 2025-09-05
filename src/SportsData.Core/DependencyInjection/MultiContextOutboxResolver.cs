using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using SportsData.Core.Eventing;

using System;

namespace SportsData.Core.DependencyInjection
{
    public interface IOutboxAmbientStateResolver
    {
        IOutboxAmbientState Get<T>() where T : DbContext;
    }


    public class MultiContextOutboxResolver : IOutboxAmbientStateResolver
    {
        private readonly IServiceProvider _provider;

        public MultiContextOutboxResolver(IServiceProvider provider)
        {
            _provider = provider;
        }

        public IOutboxAmbientState Get<T>() where T : DbContext
        {
            return _provider.GetRequiredService<EfOutboxAmbientState<T>>();
        }
    }

}
