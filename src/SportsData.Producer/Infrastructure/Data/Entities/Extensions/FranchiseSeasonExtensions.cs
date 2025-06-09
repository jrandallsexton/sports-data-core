using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class FranchiseSeasonExtensions
    {
        public static FranchiseSeason AsEntity(
            this EspnTeamSeasonDto dto,
            Guid franchiseId,
            Guid franchiseSeasonId,
            int seasonYear,
            Guid correlationId,
            Guid? venueId = null,
            Guid? groupId = null)
        {
            return new FranchiseSeason
            {
                Id = franchiseSeasonId,
                FranchiseId = franchiseId,
                VenueId = venueId,
                GroupId = groupId,
                SeasonYear = seasonYear,
                Slug = dto.Slug,
                Location = dto.Location,
                Name = dto.Name,
                Abbreviation = dto.Abbreviation,
                DisplayName = dto.DisplayName,
                DisplayNameShort = dto.ShortDisplayName,
                ColorCodeHex = dto.Color ?? string.Empty,
                ColorCodeAltHex = dto.AlternateColor,
                IsActive = dto.IsActive,
                IsAllStar = dto.IsAllStar,
                Logos = [],
                Wins = 0,
                Losses = 0,
                Ties = 0,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                ExternalIds = new List<FranchiseSeasonExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = dto.Id.ToString(),
                        UrlHash = dto.Ref.AbsoluteUri.UrlHash()
                    }
                }
            };
        }

        public static FranchiseSeasonDto ToCanonicalModel(this FranchiseSeason entity)
        {
            return new FranchiseSeasonDto()
            {
                // TODO: Implement
            };
        }
    }
}
