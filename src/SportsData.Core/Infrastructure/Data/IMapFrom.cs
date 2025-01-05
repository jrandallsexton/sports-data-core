using AutoMapper;

namespace SportsData.Core.Infrastructure.Data
{
    public interface IMapFrom<T>
    {
        void Mapping(Profile profile) => profile.CreateMap(typeof(T), GetType());
    }
}
