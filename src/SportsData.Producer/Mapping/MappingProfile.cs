using AutoMapper;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Mapping
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<Venue, VenueDto>();
            CreateMap<VenueImage, VenueImageDto>();
        }
    }
}
