using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseExtensions
    {
        public static Franchise AsEntity(
            this EspnFranchiseDto dto,
            Sport sport,
            Guid franchiseId,
            Guid correlationId)
        {
            return new Franchise()
            {
                Id = franchiseId,
                Sport = sport,
                Abbreviation = dto.Abbreviation,
                ColorCodeHex = string.IsNullOrEmpty(dto.Color) ? "ffffff" : dto.Color,
                DisplayName = dto.DisplayName,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds = [new FranchiseExternalId() { Id = Guid.NewGuid(), Value = dto.Id.ToString(), Provider = SourceDataProvider.Espn }],
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

        public static FranchiseDto ToCanonicalModel(this Franchise entity)
        {
            return new FranchiseDto()
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
    }
}
