using SportsData.Core.Common;
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
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Franchise DTO is missing its $ref property.");

            // Generate canonical ID and hash from the franchise ref
            var franchiseIdentity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new Franchise
            {
                Id = franchiseIdentity.CanonicalId,
                Sport = sport,
                Abbreviation = dto.Abbreviation,
                ColorCodeHex = string.IsNullOrEmpty(dto.Color) ? "ffffff" : dto.Color,
                DisplayName = dto.DisplayName,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds =
                [
                    new FranchiseExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = franchiseIdentity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = franchiseIdentity.UrlHash,
                        SourceUrl = franchiseIdentity.CleanUrl
                    }
                ],
                DisplayNameShort = dto.ShortDisplayName,
                IsActive = dto.IsActive,
                Name = dto.Name,
                Nickname = dto.Nickname,
                Slug = dto.Slug,
                Location = dto.Location
            };
        }

        public static FranchiseDto ToCanonicalModel(this Franchise entity)
        {
            return new FranchiseDto
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
