using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Application.Documents.Processors;

public static class ExternalEntityResolver
{
    public static async Task<Guid?> TryResolveEntityIdAsync<TEntity>(
        this DbContext db,
        string refUrl,
        SourceDataProvider provider,
        Func<DbSet<TEntity>> dbSetSelector,
        Func<TEntity, IEnumerable<ExternalId>> externalIdsSelector,
        ILogger logger = null) where TEntity : class
    {
        var urlHash = HashProvider.GenerateHashFromUrl(refUrl);

        // Materialize list and resolve in-memory if lazy-loading is off
        var entities = await dbSetSelector().ToListAsync();

        var entity = entities.FirstOrDefault(e =>
            externalIdsSelector(e).Any(x =>
                x.Provider == provider &&
                x.UrlHash == urlHash));

        if (entity == null)
        {
            logger?.LogWarning("Could not resolve {Entity} from ref: {Ref}", typeof(TEntity).Name, refUrl);
            return null;
        }

        return (Guid?)entity.GetType().GetProperty("Id")?.GetValue(entity);
    }

    public static async Task<Guid?> TryResolveFromDtoRefAsync<TEntity>(
        this DbContext db,
        IHasRef dtoRef,
        SourceDataProvider provider,
        Func<DbSet<TEntity>> dbSetSelector,
        ILogger logger = null)
        where TEntity : class, IHasExternalIds
    {
        if (dtoRef?.Ref is null)
            return null;

        return await db.TryResolveEntityIdAsync(
            dtoRef.Ref.AbsoluteUri,
            provider,
            dbSetSelector,
            entity => entity.GetExternalIds(),
            logger);
    }

}