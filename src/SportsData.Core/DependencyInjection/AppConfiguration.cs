using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

using SportsData.Core.Middleware.Health;

namespace SportsData.Core.DependencyInjection
{
    public static class AppConfiguration
    {
        public static WebApplication UseHealthChecks(this WebApplication app)
        {
            app.UseHealthChecks("/api/health", new HealthCheckOptions()
            {
                ResponseWriter = HealthCheckWriter.WriteResponse
            });
            return app;
        }
    }
}
