using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class ContestExtensions
    {
        public static FootballContest AsFootballEntity(
            this EspnEventDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Sport sport,
            int seasonYear,
            Guid? seasonWeekId,
            Guid seasonPhaseId,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new FootballContest
            {
                Id = identity.CanonicalId,
                ShortName = dto.ShortName,
                Name = dto.Name,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                StartDateUtc = DateTime.Parse(dto.Date).ToUniversalTime(),
                ExternalIds =
                [
                    new ContestExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl
                    }
                ],
                Sport = sport,
                SeasonYear = seasonYear,
                SeasonWeekId = seasonWeekId,
                SeasonPhaseId = seasonPhaseId
            };
        }

        public static BaseballContest AsBaseballEntity(
            this EspnEventDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Sport sport,
            int seasonYear,
            Guid? seasonWeekId,
            Guid seasonPhaseId,
            Guid correlationId)
        {
            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new BaseballContest
            {
                Id = identity.CanonicalId,
                ShortName = dto.ShortName,
                Name = dto.Name,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                StartDateUtc = DateTime.Parse(dto.Date).ToUniversalTime(),
                ExternalIds =
                [
                    new ContestExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = identity.UrlHash,
                        SourceUrl = identity.CleanUrl
                    }
                ],
                Sport = sport,
                SeasonYear = seasonYear,
                SeasonWeekId = seasonWeekId,
                SeasonPhaseId = seasonPhaseId
            };
        }

        public static ContestDto ToCanonicalModel(this ContestBase entity)
        {
            return new ContestDto()
            {
                Id = entity.Id,
                Name = entity.Name,
                ShortName = entity.ShortName,
                StartDateUtc = entity.StartDateUtc,
                EndDateUtc = entity.EndDateUtc,
                Sport = entity.Sport,
                SeasonYear = entity.SeasonYear,
                Week = entity.Week,
                EventNote = entity.EventNote,
                VenueId = entity.VenueId
            };
        }
    }
}
