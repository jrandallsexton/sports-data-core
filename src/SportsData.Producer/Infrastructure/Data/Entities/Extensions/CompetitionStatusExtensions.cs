using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionStatusExtensions
    {
        public static CompetitionStatus AsEntity(
            this EspnEventCompetitionStatusDto dto,
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
    }
}
