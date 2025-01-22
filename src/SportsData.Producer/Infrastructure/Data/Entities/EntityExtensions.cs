using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Models.Canonical;

using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public static class EntityExtensions
    {
        public static Franchise AsFranchiseEntity(this EspnFranchiseDto dto, Guid franchiseId, Guid correlationId)
        {
            return new Franchise()
            {
                Id = franchiseId,
                Abbreviation = dto.Abbreviation,
                ColorCodeHex = string.IsNullOrEmpty(dto.Color) ? "ffffff" : dto.Color,
                DisplayName = dto.DisplayName,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds = [new FranchiseExternalId() {Id = Guid.NewGuid(), Value = dto.Id.ToString(), Provider = SourceDataProvider.Espn }],
                DisplayNameShort = dto.ShortDisplayName,
                IsActive = dto.IsActive,
                Name = dto.Name,
                Nickname = dto.Nickname,
                Slug = dto.Slug,
                //Logos = dto.Logos.Select(x => new FranchiseLogo()
                //{
                //    Id = Guid.NewGuid(),
                //    CreatedBy = correlationId,
                //    CreatedUtc = DateTime.UtcNow,
                //    FranchiseId = franchiseId,
                //    Height = x.Height,
                //    Width = x.Width,
                //    Url = x.Href.ToString()
                //}).ToList()
            };
        }

        public static Group AsGroupEntity(this EspnGroupBySeasonDto dto, Guid groupId, Guid correlationId)
        {
            return new Group()
            {
                Id = Guid.NewGuid(),
                Abbreviation = dto.Abbreviation,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds =
                [
                    new GroupExternalId()
                    {
                        Id = Guid.NewGuid(), Value = dto.Id.ToString(),
                        Provider = SourceDataProvider.Espn
                    }
                ],
                IsConference = dto.IsConference,
                MidsizeName = dto.MidsizeName,
                Name = dto.Name,
                //ParentGroupId = espnDto.Parent. // TODO: Determine how to set/get this
                ShortName = dto.ShortName,
                Seasons = []
            };
        }

        public static GroupSeason AsGroupSeasonEntity(
            this EspnGroupBySeasonDto dto,
            Guid groupId,
            Guid groupSeasonId,
            int seasonYear,
            Guid correlationId)
        {
            return new GroupSeason()
            {
                Id = groupSeasonId,
                GroupId = groupId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                Season = seasonYear
            };
        }

        public static FranchiseCanonicalModel ToCanonicalModel(this Franchise entity)
        {
            return new FranchiseCanonicalModel()
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedUtc = entity.CreatedUtc,
                Abbreviation = entity.Abbreviation,
                ColorCodeAltHex = entity.ColorCodeAltHex,
                ColorCodeHex = entity.ColorCodeHex,
                DisplayName = entity.DisplayName,
                DisplayNameShort = entity.DisplayNameShort,
                Nickname = entity.Nickname,
                Sport = entity.Sport
            };
        }

        public static Venue AsVenueEntity(this EspnVenueDto dto, Guid venueId, Guid correlationId)
        {
            return new Venue()
            {
                Id = venueId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                IsGrass = dto.Grass,
                IsIndoor = dto.Indoor,
                Name = dto.FullName,
                ShortName = dto.ShortName
            };
        }

        public static VenueCanonicalModel ToCanonicalModel(this Venue entity)
        {
            return new VenueCanonicalModel()
            {
                Id = entity.Id,
                Name = entity.Name,
                CreatedUtc = entity.CreatedUtc,
                IsIndoor = entity.IsIndoor,
                IsGrass = entity.IsGrass,
                ShortName = entity.ShortName
            };
        }
    }
}
