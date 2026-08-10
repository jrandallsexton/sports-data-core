using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.MetricBot;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Weekly deetsMeter prediction run. Hangfire owns the schedule (and
    /// the manual-trigger button in the jobs dashboard) while MetricBot —
    /// the internal Python service — does the work and POSTs its
    /// predictions back to the API's ingestion endpoint.
    ///
    /// Chosen over a bare K8s CronJob precisely for the dashboard:
    /// on-demand and parameterized runs are the experiment workflow, and
    /// a CronJob offers neither. Design:
    /// docs/metrics-modeling/metrics-microservice-deetsmeter.md.
    /// </summary>
    public class MetricBotWeeklyJob
    {
        private readonly IProvideMetricBot _metricBot;
        private readonly ILogger<MetricBotWeeklyJob> _logger;

        /// <summary>
        /// Early-season weeks have little or no current-season data, so the
        /// feature windows are topped up with each team's last N
        /// prior-season games. Harmless later in the season: the top-up
        /// retires itself once a team has N games of its own.
        /// </summary>
        private const int PriorSeasonTail = 5;

        public MetricBotWeeklyJob(
            IProvideMetricBot metricBot,
            ILogger<MetricBotWeeklyJob> logger)
        {
            _metricBot = metricBot;
            _logger = logger;
        }

        /// <param name="sport">Platform Sport enum — MetricBot speaks the same vocabulary.</param>
        public async Task ExecuteAsync(Sport sport)
        {
            if (!MetricBotSports.IsSupported(sport))
            {
                // Football-only models — a scheduling mistake should say so
                // plainly rather than round-trip to a 422.
                throw new InvalidOperationException(
                    $"MetricBot has no model for {sport}; supported sports are " +
                    $"{Sport.FootballNcaa} and {Sport.FootballNfl}.");
            }

            _logger.LogInformation("MetricBotWeeklyJob starting. Sport: {Sport}", sport);

            var result = await _metricBot.RunWeekAsync(new MetricBotRunRequest
            {
                Sport = sport,
                PriorSeasonTail = PriorSeasonTail,
                // Live run: no explicit week (MetricBot resolves the current
                // one), publishes by default.
            });

            if (!result.IsSuccess || result.Value is null)
            {
                var errors = result is Failure<MetricBotRunResponse> failure
                    ? string.Join(", ", failure.Errors.Select(e => e.ErrorMessage))
                    : "unknown error";

                // Throw so Hangfire records a failed job (visible in the
                // dashboard, retried per its policy) rather than a silent
                // success — this is the season's weekly deliverable.
                throw new InvalidOperationException($"MetricBot run failed for {sport}: {errors}");
            }

            var run = result.Value;
            _logger.LogInformation(
                "MetricBotWeeklyJob complete. Sport: {Sport}, Season: {Season}, Week: {Week}, " +
                "Contests: {Contests}, TrainingRows: {TrainingRows}, MAE: {Mae:F2}, " +
                "ResidualStd: {ResidualStd:F2}, Published: {Published}, Elapsed: {Elapsed:F1}s",
                sport, run.SeasonYear, run.Week, run.Contests, run.TrainingRows,
                run.Mae, run.ResidualStd, run.Published, run.ElapsedSeconds);
        }
    }
}
