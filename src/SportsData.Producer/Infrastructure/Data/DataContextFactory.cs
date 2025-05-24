using SportsData.Core.Common;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Golf;

namespace SportsData.Producer.Infrastructure.Data
{
    public interface IDataContextFactory
    {
        BaseDataContext Resolve(Sport mode);
    }

    public class DataContextFactory : IDataContextFactory
    {
        private readonly IServiceProvider _provider;

        public DataContextFactory(IServiceProvider provider)
        {
            _provider = provider;
        }

        public BaseDataContext Resolve(Sport mode)
        {
            return mode switch
            {
                Sport.FootballNcaa => _provider.GetRequiredService<FootballDataContext>(),
                Sport.FootballNfl => _provider.GetRequiredService<FootballDataContext>(),
                Sport.GolfPga => _provider.GetRequiredService<GolfDataContext>(),
                _ => throw new ArgumentException($"Unsupported sport '{mode}'")
            };
        }
    }

}
