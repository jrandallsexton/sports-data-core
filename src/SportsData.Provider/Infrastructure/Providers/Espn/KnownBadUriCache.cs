using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    /// <summary>
    /// Remembers ESPN URIs that returned 400 BadRequest so repeat requests
    /// short-circuit instead of re-hitting ESPN. A 400 is a permanent
    /// "unsupported" answer (e.g. "Probabilities are not supported for ...
    /// competition: X") — unlike 404 (dependency not yet published; must
    /// keep retrying) or 403 (rate limiting; handled by the retry policy).
    /// Producer's live streamers re-request child documents on a fixed
    /// cadence for the whole game, so without this an unsupported document
    /// is refetched every cycle.
    ///
    /// Two layers: an in-memory dictionary serves the per-fetch check with
    /// zero DB reads; the <see cref="EspnKnownBadUri"/> table makes the
    /// knowledge durable — hydrated once at first use (so a fresh KEDA pod
    /// starts already knowing what every previous pod learned) and written
    /// through on each new 400.
    /// </summary>
    public interface IKnownBadUriCache
    {
        Task<bool> IsKnownBadAsync(Uri uri);
        Task MarkBadAsync(Uri uri);
    }

    public class KnownBadUriCache : IKnownBadUriCache
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(12);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<KnownBadUriCache> _logger;
        private readonly ConcurrentDictionary<string, DateTime> _expiryByKey = new();
        private readonly SemaphoreSlim _hydrateLock = new(1, 1);
        private volatile bool _hydrated;

        public KnownBadUriCache(
            IServiceScopeFactory scopeFactory,
            IDateTimeProvider dateTimeProvider,
            ILogger<KnownBadUriCache> logger)
        {
            _scopeFactory = scopeFactory;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;
        }

        public async Task<bool> IsKnownBadAsync(Uri uri)
        {
            await EnsureHydratedAsync();

            var key = HashProvider.GenerateHashFromUri(uri);
            if (!_expiryByKey.TryGetValue(key, out var expiry))
                return false;

            if (expiry > _dateTimeProvider.UtcNow())
                return true;

            _expiryByKey.TryRemove(key, out _);
            return false;
        }

        public async Task MarkBadAsync(Uri uri)
        {
            var now = _dateTimeProvider.UtcNow();
            var key = HashProvider.GenerateHashFromUri(uri);
            var expiresUtc = now.Add(Ttl);

            // Memory first — suppression works even if the write below fails.
            _expiryByKey[key] = expiresUtc;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<AppDataContext>();

                var row = await dataContext.EspnKnownBadUris
                    .FirstOrDefaultAsync(x => x.UrlHash == key);
                if (row is null)
                {
                    dataContext.EspnKnownBadUris.Add(new EspnKnownBadUri
                    {
                        UrlHash = key,
                        Uri = uri,
                        CreatedUtc = now,
                        ExpiresUtc = expiresUtc,
                    });
                }
                else
                {
                    row.ExpiresUtc = expiresUtc;
                }

                // The table stays tiny; prune expired rows opportunistically.
                var expired = await dataContext.EspnKnownBadUris
                    .Where(x => x.ExpiresUtc <= now)
                    .ToListAsync();
                dataContext.EspnKnownBadUris.RemoveRange(expired);

                await dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Another pod raced us to the same insert — its row is as
                // good as ours; memory is already updated.
                _logger.LogDebug(ex, "Concurrent EspnKnownBadUri write; keeping existing row. Uri={Uri}", uri);
            }
        }

        private async Task EnsureHydratedAsync()
        {
            if (_hydrated) return;

            await _hydrateLock.WaitAsync();
            try
            {
                if (_hydrated) return;

                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<AppDataContext>();

                var now = _dateTimeProvider.UtcNow();
                var rows = await dataContext.EspnKnownBadUris
                    .AsNoTracking()
                    .Where(x => x.ExpiresUtc > now)
                    .Select(x => new { x.UrlHash, x.ExpiresUtc })
                    .ToListAsync();

                foreach (var row in rows)
                    _expiryByKey.TryAdd(row.UrlHash, row.ExpiresUtc);

                _hydrated = true;

                if (rows.Count > 0)
                {
                    _logger.LogInformation(
                        "Hydrated {Count} known-bad ESPN URIs from the database.",
                        rows.Count);
                }
            }
            catch (Exception ex)
            {
                // Leave _hydrated false so the next call retries; suppression
                // still works for anything learned in-process meanwhile.
                _logger.LogWarning(ex, "Failed to hydrate known-bad URI cache; will retry on next check.");
            }
            finally
            {
                _hydrateLock.Release();
            }
        }
    }
}
