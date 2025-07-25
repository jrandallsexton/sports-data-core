﻿using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class ContestExtensions
    {
        public static Contest AsEntity(
            this EspnEventDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Sport sport,
            int seasonYear,
            Guid correlationId)
        {

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new Contest()
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
                        SourceUrl = dto.Ref.ToCleanUrl()
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
                EventNote = entity.EventNote,
                VenueId = entity.VenueId
            };
        }
    }
}
