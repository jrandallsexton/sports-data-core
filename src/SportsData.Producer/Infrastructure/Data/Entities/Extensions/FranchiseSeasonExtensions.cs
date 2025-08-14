using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonExtensions
    {
        public static FranchiseSeason AsEntity(
            this EspnTeamSeasonDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid franchiseId,
            int seasonYear,
            Guid correlationId,
            Guid? venueId = null,
            Guid? groupSeasonId = null)
        {
            if (dto.Ref == null)
                throw new ArgumentException($"{nameof(EspnTeamSeasonDto)} is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new FranchiseSeason
            {
                Id = identity.CanonicalId,
                FranchiseId = franchiseId,
                VenueId = venueId,
                GroupSeasonId = groupSeasonId,
                SeasonYear = seasonYear,
                Slug = dto.Slug,
                Location = dto.Location,
                Name = dto.Name,
                Abbreviation = dto.Abbreviation,
                DisplayName = dto.DisplayName,
                DisplayNameShort = dto.ShortDisplayName,
                ColorCodeHex = dto.Color,
                ColorCodeAltHex = dto.AlternateColor,
                IsActive = dto.IsActive,
                IsAllStar = dto.IsAllStar,
                Logos = [],
                Wins = 0,
                Losses = 0,
                Ties = 0,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds =
                [
                    new FranchiseSeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = identity.UrlHash,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl
                    }
                ]
            };
        }

        public static FranchiseSeasonDto ToCanonicalModel(this FranchiseSeason entity)
        {
            return new FranchiseSeasonDto
            {
                // TODO: Implement
            };
        }
    }
}
