using System.Linq.Expressions;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common; // SourceDataProvider
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts; // IHasRef

using SDExternalId = SportsData.Producer.Infrastructure.Data.Common.ExternalId;

public static class ExternalEntityResolverExtensions
{
    /// <summary>
    /// Single resolve: dtoRef -> Guid?
    /// Pushes filter to SQL via EXISTS on the ExternalIds navigation.
    /// </summary>
    public static Task<Guid?> ResolveIdAsync<TEntity, TExternalId>(
        this DbContext db,
        IHasRef dtoRef,
        SourceDataProvider provider,
        Func<IQueryable<TEntity>> set,             // e.g. () => db.Set<TEntity>()
        string externalIdsNav = "ExternalIds",     // your nav name on TEntity
        Expression<Func<TEntity, Guid>>? key = null,
        CancellationToken ct = default)
        where TEntity : class
        where TExternalId : SDExternalId
    {
        if (dtoRef?.Ref is null) return Task.FromResult<Guid?>(null);
        var hash = HashProvider.GenerateHashFromUri(dtoRef.Ref);
        return db.ResolveIdByHashAsync<TEntity, TExternalId>(hash, provider, set, externalIdsNav, key, ct);
    }

    /// <summary>
    /// Batch resolve: many refs -> { hash -> Guid } in one round-trip.
    /// Uses entity's "Id" as the key (no keySelector to keep it translatable).
    /// </summary>
    public static async Task<Dictionary<string, Guid>> ResolveIdsAsync<TEntity, TExternalId>(
        this DbContext db,
        IEnumerable<IHasRef> dtoRefs,
        SourceDataProvider provider,
        Func<IQueryable<TEntity>> set,
        string externalIdsNav = "ExternalIds",
        CancellationToken ct = default)
        where TEntity : class
        where TExternalId : SDExternalId
    {
        var hashes = dtoRefs
            .Where(r => r?.Ref != null)
            .Select(r => HashProvider.GenerateHashFromUri(r!.Ref))
            .Distinct()
            .ToArray();

        if (hashes.Length == 0)
            return new Dictionary<string, Guid>();

        // We assume a Guid primary key named "Id".
        Expression<Func<TEntity, Guid>> keySel = e => EF.Property<Guid>(e, "Id");

        return await set().AsNoTracking()
            .Where(e =>
                EF.Property<IEnumerable<TExternalId>>(e, externalIdsNav)
                  .Any(x => x.Provider == provider && hashes.Contains(x.SourceUrlHash)))
            .SelectMany(e =>
                EF.Property<IEnumerable<TExternalId>>(e, externalIdsNav)
                  .Where(x => x.Provider == provider && hashes.Contains(x.SourceUrlHash))
                  .Select(x => new { x.SourceUrlHash, Id = EF.Property<Guid>(e, "Id") }))
            .ToDictionaryAsync(k => k.SourceUrlHash, v => v.Id, ct);
    }

    // ---- private core (hash path) ----

    private static async Task<Guid?> ResolveIdByHashAsync<TEntity, TExternalId>(
        this DbContext db,
        string hash,
        SourceDataProvider provider,
        Func<IQueryable<TEntity>> set,
        string externalIdsNav,
        Expression<Func<TEntity, Guid>>? key,
        CancellationToken ct)
        where TEntity : class
        where TExternalId : SDExternalId
    {
        var keySel = key ?? (e => EF.Property<Guid>(e, "Id"));

        var id = await set().AsNoTracking()
            .Where(e =>
                EF.Property<IEnumerable<TExternalId>>(e, externalIdsNav)
                  .Any(x => x.Provider == provider && x.SourceUrlHash == hash))
            .Select(keySel)
            .SingleOrDefaultAsync(ct);

        return id == Guid.Empty ? (Guid?)null : id;
    }
}
