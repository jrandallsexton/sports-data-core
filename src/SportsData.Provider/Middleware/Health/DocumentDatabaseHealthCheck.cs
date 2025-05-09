using Microsoft.Extensions.Diagnostics.HealthChecks;

using SportsData.Provider.Infrastructure.Data;

namespace SportsData.Provider.Middleware.Health
{
    public class DocumentDatabaseHealthCheck : IHealthCheck
    {
        private readonly ILogger<DocumentDatabaseHealthCheck> _logger;
        private readonly IDocumentStore _documentStore;

        public DocumentDatabaseHealthCheck(
            ILogger<DocumentDatabaseHealthCheck> logger,
            IDocumentStore documentStore)
        {
            _logger = logger;
            _documentStore = documentStore;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = new CancellationToken())
        {
            try
            {
                var canConnect = _documentStore.CanConnect();

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
