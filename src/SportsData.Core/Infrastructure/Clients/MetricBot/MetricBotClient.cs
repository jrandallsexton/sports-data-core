using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using FluentValidation.Results;

using Microsoft.Extensions.Logging;

using SportsData.Core.Common;

namespace SportsData.Core.Infrastructure.Clients.MetricBot
{
    /// <summary>
    /// Typed client for the MetricBot service (Python/FastAPI, internal —
    /// no public ingress). Design:
    /// docs/metrics-modeling/metrics-microservice-deetsmeter.md.
    ///
    /// MetricBot itself POSTs its predictions to the API's existing
    /// ingestion endpoint; this client only triggers runs and reads back
    /// run metadata.
    /// </summary>
    public interface IProvideMetricBot
    {
        Task<Result<MetricBotRunResponse>> RunWeekAsync(
            MetricBotRunRequest request,
            CancellationToken cancellationToken = default);

        Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default);
    }

    public class MetricBotClient : IProvideMetricBot
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<MetricBotClient> _logger;

        /// <summary>
        /// Upstream failures can carry a full FastAPI traceback or an
        /// ingress HTML error page; that text reaches both the log and the
        /// admin caller, so cap it.
        /// </summary>
        private const int MaxUpstreamErrorChars = 500;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public MetricBotClient(HttpClient httpClient, ILogger<MetricBotClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<Result<MetricBotRunResponse>> RunWeekAsync(
            MetricBotRunRequest request,
            CancellationToken cancellationToken = default)
        {
            if (!MetricBotSports.IsSupported(request.Sport))
            {
                return new Failure<MetricBotRunResponse>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(request.Sport),
                        $"Unsupported sport '{request.Sport}'. MetricBot has football models only: 'ncaaf' or 'nfl'.")]);
            }

            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    "run-week", request, JsonOptions, cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    var detail = Truncate(body);

                    _logger.LogError(
                        "MetricBot run-week failed. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, detail);

                    // 422 is FastAPI's request-validation status — a caller
                    // problem, not a server fault, so surface it as one.
                    var status = response.StatusCode is System.Net.HttpStatusCode.BadRequest
                                 or System.Net.HttpStatusCode.UnprocessableEntity
                        ? ResultStatus.BadRequest
                        : ResultStatus.Error;

                    return new Failure<MetricBotRunResponse>(
                        default!,
                        status,
                        [new ValidationFailure("MetricBot", $"MetricBot returned {(int)response.StatusCode}: {detail}")]);
                }

                var result = JsonSerializer.Deserialize<MetricBotRunResponse>(body, JsonOptions);

                if (result is null)
                {
                    return new Failure<MetricBotRunResponse>(
                        default!,
                        ResultStatus.Error,
                        [new ValidationFailure("MetricBot", "MetricBot returned an unreadable response")]);
                }

                return new Success<MetricBotRunResponse>(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Caller aborted — propagate rather than reporting a server
                // fault for something the client chose to stop.
                throw;
            }
            catch (TaskCanceledException ex)
            {
                // Not caller-requested: HttpClient.Timeout elapsed.
                _logger.LogError(ex, "MetricBot run-week timed out");
                return new Failure<MetricBotRunResponse>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure("MetricBot", "MetricBot did not respond before the client timeout elapsed")]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MetricBot run-week request failed");
                return new Failure<MetricBotRunResponse>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure("MetricBot", $"MetricBot request failed: {ex.Message}")]);
            }
        }

        private static string Truncate(string? value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Length <= MaxUpstreamErrorChars
                ? value
                : value[..MaxUpstreamErrorChars] + "... (truncated)";
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            // The typed client's 10-minute timeout exists for training runs;
            // a liveness probe must fail fast instead of holding a request
            // thread while MetricBot is unreachable.
            using var probeTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            probeTimeout.CancelAfter(TimeSpan.FromSeconds(5));

            try
            {
                using var response = await _httpClient.GetAsync("health", probeTimeout.Token);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MetricBot health check failed");
                return false;
            }
        }
    }

    public static class MetricBotSports
    {
        public const string Ncaaf = "ncaaf";
        public const string Nfl = "nfl";

        public static bool IsSupported(string? sport) => sport is Ncaaf or Nfl;

        /// <summary>Maps the platform Sport enum to MetricBot's vocabulary; null when unsupported.</summary>
        public static string? FromSport(Sport sport) => sport switch
        {
            Sport.FootballNcaa => Ncaaf,
            Sport.FootballNfl => Nfl,
            _ => null  // MetricBot has football models only
        };
    }

    public class MetricBotRunRequest
    {
        /// <summary>"ncaaf" or "nfl" — MetricBot's own sport vocabulary.</summary>
        public string Sport { get; set; } = MetricBotSports.Ncaaf;

        /// <summary>Explicit (season, week) = experiment; omit both for a live run.</summary>
        public int? SeasonYear { get; set; }

        public int? Week { get; set; }

        /// <summary>Top up thin early-week feature windows with prior-season games.</summary>
        public int PriorSeasonTail { get; set; }

        /// <summary>Explicit-week runs never publish unless this is true.</summary>
        public bool Publish { get; set; }

        public bool DryRun { get; set; }

        public bool IncludeDtos { get; set; }
    }

    public class MetricBotRunResponse
    {
        public string ModelVersion { get; set; } = default!;
        public string Sport { get; set; } = default!;

        /// <summary>Always populated — live runs resolve it from the calendar.</summary>
        public int SeasonYear { get; set; }

        public int Week { get; set; }
        public int PriorSeasonTail { get; set; }
        public int TrainingRows { get; set; }
        public int Contests { get; set; }
        public double Mae { get; set; }
        public double ResidualStd { get; set; }
        public bool Published { get; set; }
        public double ElapsedSeconds { get; set; }

        /// <summary>
        /// Deliberately opaque (deserializes to JsonElement[]): these are
        /// MetricBot's own prediction DTOs, echoed back only when
        /// IncludeDtos is set. Do NOT replace with a typed model — the
        /// shape belongs to the Python side and would drift.
        /// </summary>
        public object[]? Dtos { get; set; }
    }
}
