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
    }
}
