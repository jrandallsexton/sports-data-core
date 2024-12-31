using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using Serilog;

using SportsData.Core.Middleware.Health;

namespace SportsData.Core.DependencyInjection
{
    public static class AppConfiguration
    {
        public static WebApplication UseCommonFeatures(this WebApplication app)
        {
            app.UseHealthChecks("/api/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });

            app.UseSerilogRequestLogging();
            return app;
        }
    }
}