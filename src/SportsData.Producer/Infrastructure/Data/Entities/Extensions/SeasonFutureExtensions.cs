using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class SeasonFutureExtensions
{
    public static SeasonFuture AsEntity(
        this EspnFootballSeasonFutureDto dto,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid seasonId,
        Guid correlationId,
        string urlHash,
        SourceDataProvider provider)
    {
        var identity = externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = new SeasonFuture
        {
            Id = Guid.NewGuid(),
            SeasonId = seasonId,
            Name = dto.Name,
            Type = dto.Type,
            DisplayName = dto.DisplayName,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            Items = new List<SeasonFutureItem>(),
            ExternalIds = new List<SeasonFutureExternalId>
            {
                new()
                {
                    Id = identity.CanonicalId,
                    Value = identity.UrlHash,
                    Provider = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl = identity.CleanUrl
                }
            }
        };

        // Assign the correct SeasonFutureId to ExternalIds
        foreach (var extId in entity.ExternalIds)
        {
            extId.SeasonFutureId = entity.Id;
        }

        if (dto.Futures != null)
        {
            foreach (var market in dto.Futures)
            {
                var marketEntity = new SeasonFutureItem
                {
                    Id = Guid.NewGuid(),
                    SeasonFutureId = entity.Id,
                    ProviderId = market.Provider.Id,
                    ProviderName = market.Provider.Name,
                    ProviderActive = market.Provider.Active,
                    ProviderPriority = market.Provider.Priority,
                    Books = new List<SeasonFutureBook>()  // Empty for now!
                };

                entity.Items.Add(marketEntity);
            }
        }

        return entity;
    }
}