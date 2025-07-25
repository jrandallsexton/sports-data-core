using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Application.Documents.Processors;

public static class ExternalEntityResolver
{
    public static async Task<Guid?> TryResolveEntityIdAsync<TEntity>(
        this DbContext db,
        Uri refUrl,
        SourceDataProvider provider,
        Func<IQueryable<TEntity>> querySelector,
        Func<TEntity, IEnumerable<ExternalId>> externalIdsSelector,
        ILogger? logger) where TEntity : class
    {
        var urlHash = HashProvider.GenerateHashFromUri(refUrl);

        var entities = await querySelector().ToListAsync();

        var entity = entities.FirstOrDefault(e =>
            externalIdsSelector(e).Any(x =>
                x.Provider == provider &&
                x.SourceUrlHash == urlHash));

        if (entity != null)
            return (Guid?)entity.GetType().GetProperty("Id")?.GetValue(entity);

        logger?.LogInformation("Could not resolve {Entity} from ref: {Ref}", typeof(TEntity).Name, refUrl);
        return null;
    }

    public static async Task<Guid?> TryResolveFromDtoRefAsync<TEntity>(
        this DbContext db,
        IHasRef dtoRef,
        SourceDataProvider provider,
        Func<IQueryable<TEntity>> querySelector,
        ILogger? logger)
        where TEntity : class, IHasExternalIds
    {
        if (dtoRef?.Ref is null)
            return null;

        return await db.TryResolveEntityIdAsync(
            dtoRef.Ref,
            provider,
            querySelector,
            entity => entity.GetExternalIds(),
            logger);
    }


}