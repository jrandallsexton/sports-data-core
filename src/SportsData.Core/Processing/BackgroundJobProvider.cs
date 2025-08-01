﻿using Hangfire;
using Hangfire.Server;
using Hangfire.Tags;

using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace SportsData.Core.Processing
{
    public interface IProvideBackgroundJobs
    {
        void Enqueue<T>(Expression<Func<T, Task>> methodCall); //where T : IAmABackgroundJob<T>;

        void Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context);
    }

    public class BackgroundJobProvider : IProvideBackgroundJobs
    {
        public void Enqueue<T>(Expression<Func<T, Task>> methodCall) //where T : IAmABackgroundJob<T>
        {
            BackgroundJob.Enqueue(methodCall);
        }

        public void Enqueue<T>(Expression<Func<T, Task>> methodCall, PerformContext context) //where T : IAmABackgroundJob<T>
        {
            BackgroundJob.Enqueue(methodCall);
            context.AddTags("Testing");
        }
    }
}
