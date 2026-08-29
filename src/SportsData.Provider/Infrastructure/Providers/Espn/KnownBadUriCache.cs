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
    /// <summary>Why ESPN refused the URI — drives the suppression policy.</summary>
    public enum KnownBadReason
    {
        /// <summary>
        /// 400 — a permanent "unsupported resource" verdict (e.g.
        /// "Probabilities are not supported for ... competition: X").
        /// Flat 12h suppression.
        /// </summary>
        BadRequest,

        /// <summary>
        /// 404 — the resource doesn't exist NOW but might later (next
        /// week's AP poll; an athlete page ESPN publishes late). Escalating
        /// backoff: 5m doubling per consecutive failure to a 6h cap, so a
        /// live-window race costs minutes while a truly dead URI (an
        /// athlete ESPN references in plays but never serves) decays to a
        /// few probes a day instead of one per minute.
        /// </summary>
        NotFound,
    }

    /// <summary>
    /// Remembers ESPN URIs that returned 400 or 404 so repeat requests
    /// short-circuit instead of re-hitting ESPN. Producer's dependency
    /// retry loop re-requests missing documents indefinitely (by design —
    /// at-least-once + DLQ), so without this a URI ESPN will never serve
    /// is refetched forever (observed: one athlete page, ~1,400 asks/day).
    /// Suppression here does NOT break the DLQ flow: Producer keeps
    /// retrying its documents; Provider just answers "still nothing" from
    /// memory until the backoff window lapses and a real probe runs.
    ///
    /// Two layers: an in-memory dictionary serves the per-fetch check with
    /// zero DB reads; the <see cref="EspnKnownBadUri"/> table makes the
    /// knowledge durable — hydrated once at first use (so a fresh KEDA pod
    /// starts already knowing what every previous pod learned, including
    /// backoff escalation) and written through on each new failure.
    /// </summary>
    public interface IKnownBadUriCache
    {
        Task<bool> IsKnownBadAsync(Uri uri);
        Task MarkBadAsync(Uri uri, KnownBadReason reason);
    }

    public class KnownBadUriCache : IKnownBadUriCache
    {
        private static readonly TimeSpan BadRequestTtl = TimeSpan.FromHours(12);
        private static readonly TimeSpan NotFoundBaseTtl = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan NotFoundMaxTtl = TimeSpan.FromHours(6);

        // Expired rows are kept this long so the NotFound FailureCount
        // survives between probes (pruning at expiry would reset every
        // backoff to 5 minutes on its next failure).
        private static readonly TimeSpan PruneGrace = TimeSpan.FromDays(7);

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

        public async Task MarkBadAsync(Uri uri, KnownBadReason reason)
        {
            var now = _dateTimeProvider.UtcNow();
            var key = HashProvider.GenerateHashFromUri(uri);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dataContext = scope.ServiceProvider.GetRequiredService<AppDataContext>();

                var row = await dataContext.EspnKnownBadUris
                    .FirstOrDefaultAsync(x => x.UrlHash == key);

                // A reason change restarts the escalation: the count only
                // means "consecutive failures OF THIS KIND" — a URI that
                // 400'd for weeks then starts 404ing must begin at the
                // 5-minute base, not inherit a near-cap count.
                var reasonText = reason.ToString();
                var failureCount = row is null || row.Reason != reasonText
                    ? 1
                    : row.FailureCount + 1;
                var expiresUtc = now.Add(TtlFor(reason, failureCount));

                if (row is null)
                {
                    dataContext.EspnKnownBadUris.Add(new EspnKnownBadUri
                    {
                        UrlHash = key,
                        Uri = uri,
                        Reason = reasonText,
                        FailureCount = failureCount,
                        CreatedUtc = now,
                        ExpiresUtc = expiresUtc,
                    });
                }
                else
                {
                    row.Reason = reasonText;
                    row.FailureCount = failureCount;
                    row.ExpiresUtc = expiresUtc;
                }

                // Memory before SaveChanges so suppression holds even if the
                // write races another pod; corrected below on failure paths.
                _expiryByKey[key] = expiresUtc;

                // Prune rows expired past the grace window (NOT at expiry —
                // FailureCount must survive between probes for the backoff
                // to escalate). The current key can't match: its ExpiresUtc
                // was just set in the future, and EF's identity map returns
                // the tracked, updated instance.
                var pruneBefore = now - PruneGrace;
                var expired = await dataContext.EspnKnownBadUris
                    .Where(x => x.ExpiresUtc <= pruneBefore && x.UrlHash != key)
                    .ToListAsync();
                dataContext.EspnKnownBadUris.RemoveRange(expired);

                await dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Another pod raced us to the same insert — its row is as
                // good as ours; keep a base-TTL memory entry.
                _expiryByKey[key] = now.Add(TtlFor(reason, 1));
                _logger.LogDebug(ex, "Concurrent EspnKnownBadUri write; keeping existing row. Uri={Uri}", uri);
            }
            catch (Exception ex)
            {
                // DB unavailable — suppress in-memory at base TTL anyway;
                // durability catches up on the next failure after recovery.
                _expiryByKey[key] = now.Add(TtlFor(reason, 1));
                _logger.LogWarning(ex, "Failed to persist known-bad URI; suppressing in-memory only. Uri={Uri}", uri);
            }
        }

        private static TimeSpan TtlFor(KnownBadReason reason, int failureCount)
        {
            if (reason == KnownBadReason.BadRequest)
                return BadRequestTtl;

            // 5m, 10m, 20m, 40m, 80m, 160m, 320m, then capped at 6h.
            var exponent = Math.Clamp(failureCount - 1, 0, 30);
            var ticks = NotFoundBaseTtl.Ticks * (1L << exponent);
            return ticks >= NotFoundMaxTtl.Ticks ? NotFoundMaxTtl : TimeSpan.FromTicks(ticks);
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
