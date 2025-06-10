using MediatR;

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Middleware
{
    /// <summary>
    /// https://mehmetozkaya.medium.com/mediatr-pipeline-behaviors-and-fluent-validation-in-net-8-microservices-363e3d464433
    /// </summary>
    /// <typeparam name="TRequest"></typeparam>
    /// <typeparam name="TResponse"></typeparam>
    public class PerformanceBehaviour<TRequest, TResponse> :
        IPipelineBehavior<TRequest, TResponse> where TRequest : notnull
    {
        public async Task<TResponse> Handle(
            TRequest request,
            RequestHandlerDelegate<TResponse> next,
            CancellationToken cancellationToken)
        {
            // Pre-request logic: Start timer
            var timer = Stopwatch.StartNew();
            // Proceed with the handler
            var response = await next();
            // Post-request logic: Log elapsed time
            timer.Stop();
            var elapsedMilliseconds = timer.ElapsedMilliseconds;
            // Log or handle the timing information
            return response;
        }
    }
}
