using SportsData.Core.Common;

using System;
using System.Collections.Concurrent;

namespace SportsData.Provider.Infrastructure.Providers.Espn
{
    /// <summary>
    /// Remembers which immutable in-season items the live-index fan-out has
    /// already handed to Hangfire, so subsequent polling cycles skip them at
    /// the enqueue site — no Hangfire job, no Mongo read, no re-publish to
    /// Producer. Without this, every cycle re-enqueues every item the game
    /// has ever produced (observed 2026-09-06: 1.31M play documents processed
    /// for ~500 real plays — quadratic cumulative cost per game).
    ///
    /// Deliberately per-pod and in-memory (no durable table, unlike
    /// <see cref="KnownBadUriCache"/>): a cold cache costs exactly one
    /// full-index cycle — the pre-fix steady state — after which the pod
    /// converges. "Seen" means "enqueued once"; Hangfire owns delivery from
    /// there with its own retries, and a permanently failed item remains
    /// recoverable via the dependency-request leaf path, TTL expiry, or
    /// manual reenrich. See docs/features/live-sourcing-already-seen-skip.md.
    /// </summary>
    public interface ISeenUriCache
    {
        bool IsSeen(string urlHash);
        void MarkSeen(string urlHash);
    }

    public class SeenUriCache : ISeenUriCache
    {
        // Comfortably beyond MaxStreamDuration (5h) so no live stream ever
        // sees its own entries expire mid-game.
        private static readonly TimeSpan Ttl = TimeSpan.FromHours(8);

        // Entries for finished games are never read again (the stream stops
        // asking), so remove-on-read alone would leak; a periodic sweep keeps
        // a long-lived pod bounded.
        private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);

        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ConcurrentDictionary<string, DateTime> _expiryByKey = new();
        private DateTime _nextSweepUtc = DateTime.MinValue;

        public SeenUriCache(IDateTimeProvider dateTimeProvider)
        {
            _dateTimeProvider = dateTimeProvider;
        }

        public bool IsSeen(string urlHash)
        {
            if (!_expiryByKey.TryGetValue(urlHash, out var expiry))
                return false;

            if (expiry > _dateTimeProvider.UtcNow())
                return true;

            _expiryByKey.TryRemove(urlHash, out _);
            return false;
        }

        public void MarkSeen(string urlHash)
        {
            var now = _dateTimeProvider.UtcNow();
            _expiryByKey[urlHash] = now.Add(Ttl);

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
