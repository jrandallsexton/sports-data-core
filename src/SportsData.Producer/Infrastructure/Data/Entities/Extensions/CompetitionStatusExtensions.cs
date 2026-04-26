using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionStatusExtensions
    {
        public static CompetitionStatus AsEntity(
            this EspnEventCompetitionStatusDtoBase dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Status DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new CompetitionStatus
            {
                Id = identity.CanonicalId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,

                CompetitionId = competitionId,

                Clock = dto.Clock,
                DisplayClock = dto.DisplayClock,
                Period = dto.Period,

                StatusTypeId = dto.Type?.Id ?? string.Empty,
                StatusTypeName = dto.Type?.Name ?? string.Empty,
                StatusState = dto.Type?.State ?? string.Empty,
                IsCompleted = dto.Type?.Completed ?? false,
                StatusDescription = dto.Type?.Description ?? string.Empty,
                StatusDetail = dto.Type?.Detail ?? string.Empty,
                StatusShortDetail = dto.Type?.ShortDetail ?? string.Empty,

                ExternalIds = new List<CompetitionStatusExternalId>
                {
                    new()
                    {
                        Id = identity.CanonicalId,
                        Provider = SourceDataProvider.Espn,
                        Value = identity.UrlHash,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };
        }

        // Baseball-specific overload: layers HalfInning + PeriodPrefix +
        // FeaturedAthletes on top of the shared mapping. Football never
        // calls this path, so its rows leave the MLB columns null and the
        // FeaturedAthletes collection empty.
        //
        // createdUtc is threaded in (rather than reading DateTime.UtcNow
        // here) so the extension stays pure and unit-testable. Callers
        // pass IDateTimeProvider.UtcNow() — the project's standing rule
        // (see CLAUDE.md) for any production-side timestamping.
        public static CompetitionStatus AsEntity(
            this EspnBaseballEventCompetitionStatusDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid correlationId,
            DateTime createdUtc)
        {
            var entity = ((EspnEventCompetitionStatusDtoBase)dto).AsEntity(
                externalRefIdentityGenerator,
                competitionId,
                correlationId);

            entity.HalfInning = dto.HalfInning;
            entity.PeriodPrefix = dto.PeriodPrefix;

            if (dto.FeaturedAthletes is { Count: > 0 })
            {
                // Index-based Ordinal preserves ESPN's source order
                // (e.g. winningPitcher [0], losingPitcher [1]) so the
                // sequence survives a save+reload round-trip.
                entity.FeaturedAthletes = dto.FeaturedAthletes
                    .Select((a, i) => new CompetitionStatusFeaturedAthlete
                    {
                        Id = Guid.NewGuid(),
                        CreatedBy = correlationId,
                        CreatedUtc = createdUtc,

                        Ordinal = i,
                        PlayerId = a.PlayerId,
                        Name = a.Name,
                        DisplayName = a.DisplayName,
                        ShortDisplayName = a.ShortDisplayName,
                        Abbreviation = a.Abbreviation,
                        AthleteRef = a.Athlete?.Ref,
                        TeamRef = a.Team?.Ref,
                        StatisticsRef = a.Statistics?.Ref
                    })
                    .ToList();
            }

            return entity;
        }
    }
}
