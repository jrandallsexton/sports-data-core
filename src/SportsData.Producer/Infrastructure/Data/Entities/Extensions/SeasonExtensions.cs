using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class SeasonExtensions
    {
        public static Season AsEntity(
            this EspnFootballSeasonDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Season DTO is missing its $ref property.");

            // Generate canonical ID and hash from the season ref
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var season = new Season
            {
                Id = identity.CanonicalId,
                Year = dto.Year,
                Name = dto.DisplayName,
                StartDate = DateTime.Parse(dto.StartDate).ToUniversalTime(),
                EndDate = DateTime.Parse(dto.EndDate).ToUniversalTime(),
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,
                ExternalIds =
                {
                    new SeasonExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };

            // Add phases
            if (dto.Types?.Items != null)
            {
                foreach (var type in dto.Types.Items)
                {
                    if (type?.Ref is null)
                        continue;

                    var phaseIdentity = externalRefIdentityGenerator.Generate(type.Ref);

                    var phase = new SeasonPhase
                    {
                        Id = phaseIdentity.CanonicalId,
                        SeasonId = identity.CanonicalId,
                        TypeCode = type.Type,
                        Name = type.Name,
                        Abbreviation = type.Abbreviation,
                        Slug = type.Slug,
                        Year = type.Year,
                        StartDate = DateTime.Parse(type.StartDate).ToUniversalTime(),
                        EndDate = DateTime.Parse(type.EndDate).ToUniversalTime(),
                        HasGroups = type.HasGroups,
                        HasStandings = type.HasStandings,
                        HasLegs = type.HasLegs,
                        CreatedBy = correlationId,
                        CreatedUtc = DateTime.UtcNow,
                        ExternalIds =
                        {
                            new SeasonPhaseExternalId
                            {
                                Id = phaseIdentity.CanonicalId,
                                Value = phaseIdentity.UrlHash,
                                SourceUrl = phaseIdentity.CleanUrl,
                                Provider = SourceDataProvider.Espn,
                                SourceUrlHash = phaseIdentity.UrlHash
                            }
                        }
                    };

                    season.Phases.Add(phase);
                }
            }

            // Set ActivePhaseId by matching the hash of dto.Type.Ref
            if (dto.Type?.Ref != null)
            {
                var activePhaseIdentity = externalRefIdentityGenerator.Generate(dto.Type.Ref);

                if (season.Phases.Any(p => p.Id == activePhaseIdentity.CanonicalId))
                {
                    season.ActivePhaseId = activePhaseIdentity.CanonicalId;
                }
            }

            return season;
        }
    }
}
