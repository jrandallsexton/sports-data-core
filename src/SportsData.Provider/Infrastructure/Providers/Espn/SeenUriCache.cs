using SportsData.Core.Common;

using System;
using System.Collections.Concurrent;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    /// <summary>
    /// Short-TTL, per-pod, atomic in-flight claim for the live-index
    /// already-seen skip (L1). A claim means "this pod handed the item to
    /// Hangfire within the last few minutes — don't enqueue it again while
    /// that job is still landing." The durable, cross-pod skip signal is L2
    /// (document existence in Mongo, consulted every cycle); L1 only bridges
    /// the window between enqueue and persistence.
    ///
    /// The TTL is deliberately SHORT: a claimed item whose Hangfire job never
    /// persists (e.g. ESPN 404 → known-bad → clean return, no retry) is
    /// re-enqueued when the claim lapses, restoring the pre-skip self-healing
    /// cadence instead of masking the item for hours. The claim is atomic
    /// (test-and-set), so concurrent DocumentRequested deliveries for the
    /// same index cannot double-enqueue an item.
    /// See docs/features/live-sourcing-already-seen-skip.md.
    /// </summary>
    public interface ISeenUriCache
    {
        /// <summary>
        /// Atomically claims the hash. Returns true when the caller now holds
        /// the claim (no live claim existed) and should enqueue; false when an
        /// unexpired claim already exists and the item should be skipped.
        /// </summary>
        bool TryMarkSeen(string urlHash);
    }

    public class SeenUriCache : ISeenUriCache
    {
        // Long enough for a live-queue backlog to drain a pending job; short
        // enough that a job which never persisted re-enqueues within minutes
        // (L2 then vouches for items that DID persist, so a lapsed claim on a
        // healthy item costs nothing — the Mongo existence check skips it).
        private static readonly TimeSpan ClaimTtl = TimeSpan.FromMinutes(10);

        // Entries for finished games are never claimed again, so a periodic
        // sweep keeps a long-lived pod bounded.
        private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ConcurrentDictionary<string, DateTime> _expiryByKey = new();
        private DateTime _nextSweepUtc = DateTime.MinValue;

        public SeenUriCache(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;
        }

        public bool TryMarkSeen(string urlHash)
        {
            var now = _dateTimeProvider.UtcNow();
            var expiry = now.Add(ClaimTtl);

            SweepIfDue(now);

            while (true)
            {
                if (_expiryByKey.TryAdd(urlHash, expiry))
                    return true;

                if (!_expiryByKey.TryGetValue(urlHash, out var existing))
                    continue; // removed concurrently — retry the add

                if (existing > now)
                    return false; // live claim held (by a prior cycle or a concurrent consumer)

                // Expired claim: take it over atomically; on a lost race,
                // loop and re-evaluate against the winner's entry.
                if (_expiryByKey.TryUpdate(urlHash, expiry, existing))
                    return true;
            }
        }

        private void SweepIfDue(DateTime now)
        {
            if (now < _nextSweepUtc)
                return;

            _nextSweepUtc = now.Add(SweepInterval);
            foreach (var entry in _expiryByKey)
            {
                if (entry.Value <= now)
                    _expiryByKey.TryRemove(entry.Key, out _);
            }
        }
    }
}
