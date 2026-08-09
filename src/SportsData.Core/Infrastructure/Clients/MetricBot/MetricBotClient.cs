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
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(
                    "run-week", request, JsonOptions, cancellationToken);

                var body = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "MetricBot run-week failed. Status: {StatusCode}, Body: {Body}",
                        response.StatusCode, body);

                    return new Failure<MetricBotRunResponse>(
                        default!,
                        response.StatusCode == System.Net.HttpStatusCode.BadRequest
                            ? ResultStatus.BadRequest
                            : ResultStatus.Error,
                        [new ValidationFailure("MetricBot", $"MetricBot returned {(int)response.StatusCode}: {body}")]);
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "MetricBot run-week request failed");
                return new Failure<MetricBotRunResponse>(
                    default!,
                    ResultStatus.Error,
                    [new ValidationFailure("MetricBot", $"MetricBot request failed: {ex.Message}")]);
            }
        }

        public async Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                using var response = await _httpClient.GetAsync("health", cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "MetricBot health check failed");
                return false;
            }
        }
    }

    public class MetricBotRunRequest
    {
        /// <summary>"ncaaf" or "nfl" — MetricBot's own sport vocabulary.</summary>
        public string Sport { get; set; } = "ncaaf";

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
        public int? SeasonYear { get; set; }
        public int Week { get; set; }
        public int PriorSeasonTail { get; set; }
        public int TrainingRows { get; set; }
        public int Contests { get; set; }
        public double Mae { get; set; }
        public double ResidualStd { get; set; }
        public bool Published { get; set; }
        public double ElapsedSeconds { get; set; }
        public object[]? Dtos { get; set; }
    }
}
