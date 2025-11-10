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
        public string Enqueue<T>(Expression<Func<T, Task>> methodCall) //where T : IAmABackgroundJob<T>
        {
            return BackgroundJob.Enqueue(methodCall);
        }

        public string Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context) //where T : IAmABackgroundJob<T>
        {
            context.AddTags("Testing");
            return BackgroundJob.Enqueue(methodCall);
        }

        public string Schedule<T>(Expression<Func<T, Task>> methodCall, TimeSpan delay)
        {
            return BackgroundJob.Schedule(methodCall, delay);
        }
    }
}
