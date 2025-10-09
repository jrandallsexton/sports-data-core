using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionSituationExtensions
    {
        public static CompetitionSituation AsEntity(
            this EspnEventCompetitionSituationDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid? lastPlayId,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Situation DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new CompetitionSituation
            {
                Id = identity.CanonicalId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,

                CompetitionId = competitionId,
                LastPlayId = lastPlayId,

                Down = dto.Down,
                Distance = dto.Distance,
                YardLine = dto.YardLine,
                IsRedZone = dto.IsRedZone,
                HomeTimeouts = dto.HomeTimeouts,
                AwayTimeouts = dto.AwayTimeouts
            };
        }
    }
}
