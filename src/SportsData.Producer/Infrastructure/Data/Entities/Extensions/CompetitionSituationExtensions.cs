using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionSituationExtensions
    {
        public static FootballCompetitionSituation AsEntity(
            this EspnFootballEventCompetitionSituationDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid? lastPlayId,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Situation DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new FootballCompetitionSituation
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
                HomeTimeouts = dto.HomeTimeouts < 0 ? 0 : dto.HomeTimeouts, // actual issue found from ESPN data
                AwayTimeouts = dto.AwayTimeouts < 0 ? 0 : dto.AwayTimeouts,
            };
        }
    }
}
