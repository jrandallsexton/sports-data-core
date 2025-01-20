using Hangfire;

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SportsData.Core.Processing
{
    public interface IProvideBackgroundJobs
    {
        void Enqueue<T>(Expression<Func<T, Task>> methodCall); //where T : IAmABackgroundJob<T>;
    }

    public class BackgroundJobProvider : IProvideBackgroundJobs
    {
        public void Enqueue<T>(Expression<Func<T, Task>> methodCall) //where T : IAmABackgroundJob<T>
        {
            BackgroundJob.Enqueue(methodCall);
        }
    }
}
