using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class CompetitionProbabilityExtensions
    {
        public static CompetitionProbability AsEntity(
            this EspnEventCompetitionProbabilityDto dto,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            Guid competitionId,
            Guid? playId,
            Guid correlationId)
        {
            if (dto.Ref == null)
                throw new ArgumentException("Probability DTO is missing its $ref property.");

            var identity = externalRefIdentityGenerator.Generate(dto.Ref);

            return new CompetitionProbability
            {
                Id = identity.CanonicalId,
                CreatedBy = correlationId,
                CreatedUtc = DateTime.UtcNow,

                CompetitionId = competitionId,
                PlayId = playId,

                HomeWinPercentage = dto.HomeWinPercentage,
                AwayWinPercentage = dto.AwayWinPercentage,
                TiePercentage = dto.TiePercentage,
                SecondsLeft = dto.SecondsLeft,

                SequenceNumber = dto.SequenceNumber,
                LastModifiedRaw = dto.LastModified,

                SourceId = dto.Source?.Id ?? string.Empty,
                SourceDescription = dto.Source?.Description ?? string.Empty,
                SourceState = dto.Source?.State ?? string.Empty,

                ExternalIds = new List<CompetitionProbabilityExternalId>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        Value = identity.UrlHash,
                        Provider = SourceDataProvider.Espn,
                        SourceUrl = identity.CleanUrl,
                        SourceUrlHash = identity.UrlHash
                    }
                }
            };
        }

    }
}
