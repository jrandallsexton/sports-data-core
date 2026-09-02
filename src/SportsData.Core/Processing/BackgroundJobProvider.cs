using Hangfire;
using Hangfire.Server;
using Hangfire.Tags;

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SportsData.Core.Processing
{
    /// <summary>
    /// Hangfire queue names. PRIORITY IS ALPHABETICAL, NOT ARRAY ORDER:
    /// Hangfire.PostgreSql's dequeue is
    ///   ORDER BY "fetchedat" NULLS FIRST, "queue", "jobid"
    /// so among available jobs the alphabetically-first queue NAME wins —
    /// the configured queue array only filters, it does not rank (that's
    /// Hangfire.SqlServer semantics, which do NOT apply here). The "00-"
    /// prefix is therefore load-bearing: it is what makes live work dequeue
    /// before "daemon"/"default". Caught in review of PR #709 — the first
    /// cut used "live", which sorts AFTER "default" and would have inverted
    /// the priority. Any future queue must be named with its sort position
    /// in mind. "00-live" carries streamer-originated documents for contests
    /// backing a pick'em league — see docs/features/athlete-cascade-scoping.md
    /// (item 5: a league game must never starve behind bulk backfill).
    /// </summary>
    public static class HangfireQueues
    {
        public const string Live = "00-live";
        public const string Default = "default";
        public const string Daemon = "daemon";
    }

    public interface IProvideBackgroundJobs
    {
        string Enqueue<T>(Expression<Func<T, Task>> methodCall); //where T : IAmABackgroundJob<T>;

        /// <summary>
        /// Enqueue to a specific queue. Hangfire 1.8 sticky queues: the queue
        /// persists across state transitions, so retries and reschedules stay
        /// on the queue they were born on.
        /// </summary>
        string Enqueue<T>(string queue, Expression<Func<T, Task>> methodCall);

        string Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context);

        string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay);

        /// <summary>Queue-targeted schedule; queue sticks through the delayed->enqueued transition (Hangfire 1.8).</summary>
        string Schedule<T>(string queue, Expression<Func<T, Task>> methodCall, TimeSpan delay);

        /// <summary>
        /// Transitions the specified job to the Deleted state, preventing it from running.
        /// Used by reschedule paths (e.g. game-time changes) where an old scheduled job
        /// must be cancelled before a replacement is enqueued. No-op if the job is already
        /// in a terminal or executing state — Hangfire's state machine rejects the transition
        /// and returns false; we surface that to the caller.
        /// </summary>
        /// <returns>True if the job was transitioned to Deleted; false otherwise.</returns>
        bool Delete(string jobId);
    }

    public class BackgroundJobProvider : IProvideBackgroundJobs
    {
        private readonly IBackgroundJobClient _client;

        public BackgroundJobProvider(IBackgroundJobClient client)
        {
            _client = client;
        }

        public string Enqueue<T>(Expression<Func<T, Task>> methodCall) //where T : IAmABackgroundJob<T>
        {
            return _client.Enqueue(methodCall);
        }

        public string Enqueue<T>(string queue, Expression<Func<T, Task>> methodCall)
        {
            return _client.Enqueue(queue, methodCall);
        }

        public string Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context) //where T : IAmABackgroundJob<T>
        {
            context.AddTags("Testing");
            return _client.Enqueue(methodCall);
        }

        public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
        {
            return _client.Schedule(methodCall, delay);
        }

        public string Schedule<T>(string queue, Expression<Func<T, Task>> methodCall, TimeSpan delay)
        {
            return _client.Schedule(queue, methodCall, delay);
        }

        public bool Delete(string jobId)
        {
            return _client.Delete(jobId);
        }
    }
}
