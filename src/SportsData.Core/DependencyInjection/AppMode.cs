using SportsData.Core.Common;

namespace SportsData.Core.DependencyInjection
{
    public interface IAppMode
    {
        Sport CurrentSport { get; }
    }

    public class AppMode : IAppMode
    {
        public Sport CurrentSport { get; }

        public AppMode(Sport sport)
        {
            CurrentSport = sport;
        }
    }

}
