using SportsData.Core.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class SeasonFutureExtensions
{
    public static SeasonFuture AsEntity(
        this EspnFootballSeasonFutureDto dto,
        Guid seasonId,
        Guid correlationId,
        string urlHash,
        SourceDataProvider provider)
    {
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
                new SeasonFutureExternalId
                {
                    Id = Guid.NewGuid(),
                    SeasonFutureId = Guid.Empty,  // Temporary
                    Provider = provider,
                    Value = urlHash,
                    SourceUrlHash = urlHash,
                    CreatedBy = correlationId,
                    CreatedUtc = DateTime.UtcNow
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