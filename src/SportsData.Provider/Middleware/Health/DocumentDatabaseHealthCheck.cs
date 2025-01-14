using Microsoft.Extensions.Diagnostics.HealthChecks;

using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Middleware.Health
{
    public class DocumentDatabaseHealthCheck : IHealthCheck
    {
        private readonly ILogger<DocumentDatabaseHealthCheck> _logger;
        private readonly DocumentService dataService;

        public DocumentDatabaseHealthCheck(
            ILogger<DocumentDatabaseHealthCheck> logger,
            DocumentService dataService)
        {
            _logger = logger;
            this.dataService = dataService;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            _logger.LogInformation("Begin HC {@hc}", nameof(DocumentDatabaseHealthCheck));

            try
            {
                var canConnect = !string.IsNullOrEmpty(dataService.Database.DatabaseNamespace.DatabaseName);

                _logger.LogInformation($"{nameof(DocumentDatabaseHealthCheck)} canConnect? {canConnect}");
                var result = canConnect ?
                    HealthCheckResult.Healthy() :
                    HealthCheckResult.Unhealthy("Unable to connect to document database");

                return await Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Unable to connect to document database", ex);
            }
        }
    }
}
