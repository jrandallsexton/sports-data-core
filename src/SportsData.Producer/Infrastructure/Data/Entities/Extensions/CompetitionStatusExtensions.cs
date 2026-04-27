using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionStatusExtensions
    {
        // Football overload. Builds the concrete FootballCompetitionStatus
        // subclass — the abstract CompetitionStatus base can't be
        // instantiated directly. NCAAFB and NFL share this path; if those
        // ever diverge, split into NcaaFootballCompetitionStatus /
        // NflFootballCompetitionStatus subclasses without changing the
        // common processor.
        public static FootballCompetitionStatus AsEntity(
            this EspnEventCompetitionStatusDtoBase dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Status DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new FootballCompetitionStatus
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

        // Baseball overload. Builds BaseballCompetitionStatus so the MLB
        // fields and FeaturedAthletes children travel with the entity.
        // The shared mapping is duplicated rather than calling the
        // football overload and casting — we'd get the wrong runtime
        // type for EF and lose the discriminator. createdUtc threads in
        // (rather than reading DateTime.UtcNow here) so the extension
        // stays pure and unit-testable; callers pass
        // IDateTimeProvider.UtcNow().
        public static BaseballCompetitionStatus AsEntity(
            this EspnBaseballEventCompetitionStatusDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid correlationId,
            DateTime createdUtc)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Status DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            var entity = new BaseballCompetitionStatus
            {
                Id = identity.CanonicalId,
                CreatedBy = correlationId,
                CreatedUtc = createdUtc,

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

                HalfInning = dto.HalfInning,
                PeriodPrefix = dto.PeriodPrefix,

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

            if (dto.FeaturedAthletes is { Count: > 0 })
            {
                // Index-based Ordinal preserves ESPN's source order
                // (winningPitcher [0], losingPitcher [1] post-game) so
                // the sequence survives a save+reload round-trip.
                entity.FeaturedAthletes = dto.FeaturedAthletes
                    .Select((a, i) => new BaseballCompetitionStatusFeaturedAthlete
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
