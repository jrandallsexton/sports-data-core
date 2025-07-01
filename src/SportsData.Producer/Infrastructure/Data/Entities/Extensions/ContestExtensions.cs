using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class ContestExtensions
    {
        public static Contest AsEntity(
            this EspnEventDto dto,
            Sport sport,
            int seasonYear,
            Guid contestId,
            Guid correlationId)
        {
            var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);
            return new Contest()
            {
                Id = contestId,
                ShortName = dto.ShortName,
                Name = dto.Name,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId,
                StartDateUtc = DateTime.Parse(dto.Date),
                ExternalIds =
                [
                    new ContestExternalId
                    {
                        Id = Guid.NewGuid(),
                        Value = sourceUrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrlHash = sourceUrlHash
                    }
                ],
                Sport = sport,
                SeasonYear = seasonYear,
                Status = ContestStatus.Undefined
            };
        }

        public static ContestDto ToCanonicalModel(this Contest entity)
        {
            return new ContestDto()
            {
                Id = entity.Id,
                Name = entity.Name,
                ShortName = entity.ShortName,
                StartDateUtc = entity.StartDateUtc,
                EndDateUtc = entity.EndDateUtc,
                Status = entity.Status,
                Sport = entity.Sport,
                SeasonYear = entity.SeasonYear,
                Week = entity.Week,
                NeutralSite = entity.NeutralSite,
                Attendance = entity.Attendance,
                EventNote = entity.EventNote,
                VenueId = entity.VenueId
            };
        }
    }
}
