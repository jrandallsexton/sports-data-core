﻿using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

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
            var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);
            return new Franchise()
            {
                Id = franchiseId,
                Sport = sport,
                Abbreviation = dto.Abbreviation,
                ColorCodeHex = string.IsNullOrEmpty(dto.Color) ? "ffffff" : dto.Color,
                DisplayName = dto.DisplayName,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds = [ new FranchiseExternalId()
                {
                    Id = Guid.NewGuid(),
                    Value = sourceUrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = sourceUrlHash
                }],
                DisplayNameShort = dto.ShortDisplayName,
                IsActive = dto.IsActive,
                Name = dto.Name,
                Nickname = dto.Nickname,
                Slug = dto.Slug,
                Location = dto.Location,
                
                //Logos = dto.Logos.Select(x => new FranchiseLogo()
                //{
                //    Id = Guid.NewGuid(),
                //    CreatedBy = correlationId,
                //    CreatedUtc = DateTime.UtcNow,
                //    FranchiseId = franchiseId,
                //    Height = x.Height,
                //    Width = x.Width,
                //    Uri = x.Ref.ToString()
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
                Abbreviation = entity.Abbreviation ?? string.Empty,
                ColorCodeAltHex = entity.ColorCodeAltHex,
                ColorCodeHex = entity.ColorCodeHex,
                DisplayName = entity.DisplayName,
                DisplayNameShort = entity.DisplayNameShort,
                Nickname = entity.Nickname ?? string.Empty,
                Sport = entity.Sport
            };
        }
    }
}
