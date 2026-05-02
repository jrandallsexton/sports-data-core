using Hangfire;
using Hangfire.Server;
using Hangfire.Tags;

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SportsData.Core.Processing
{
    public interface IProvideBackgroundJobs
    {
        string Enqueue<T>(Expression<Func<T, Task>> methodCall); //where T : IAmABackgroundJob<T>;

        string Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context);

        string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay);

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

        public string Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context) //where T : IAmABackgroundJob<T>
        {
            context.AddTags("Testing");
            return _client.Enqueue(methodCall);
        }

        public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
        {
            return _client.Schedule(methodCall, delay);
        }

        public bool Delete(string jobId)
        {
            return _client.Delete(jobId);
        }
    }
}
