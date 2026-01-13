using FluentValidation.Results;

using SportsData.Core.Common;
using SportsData.Core.Extensions;
using SportsData.Core.Middleware.Health;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

public abstract class ClientBase(HttpClient httpClient) : IProvideHealthChecks
{
    protected readonly HttpClient HttpClient = httpClient;

    protected static ValidationFailure? ValidatePagination(int pageNumber, int pageSize, int maxPageSize = 500)
    {
        if (pageNumber < 1)
        {
            return new ValidationFailure("pageNumber", "Page number must be greater than or equal to 1");
        }

        if (pageSize < 1)
        {
            return new ValidationFailure("pageSize", "Page size must be greater than or equal to 1");
        }

        if (pageSize > maxPageSize)
        {
            return new ValidationFailure("pageSize", $"Page size must not exceed {maxPageSize}");
        }

        return null;
    }

    /// <summary>
    /// Performs an HTTP GET request and handles response deserialization with standard error handling.
    /// </summary>
    /// <typeparam name="TResponse">The response type to return</typeparam>
    /// <typeparam name="TDto">The DTO type to deserialize from the response body</typeparam>
    /// <param name="url">The URL to request</param>
    /// <param name="mapToResponse">Function to map the deserialized DTO to the response type</param>
    /// <param name="defaultResponse">Default response to use in failure cases</param>
    /// <param name="entityName">Entity name used in error messages (default: "Response")</param>
    /// <param name="defaultFailureStatus">Status to use when failure response cannot be parsed (default: BadRequest)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task<Result<TResponse>> GetAsync<TResponse, TDto>(
        string url,
        Func<TDto, TResponse> mapToResponse,
        TResponse defaultResponse,
        string entityName = "Response",
        ResultStatus defaultFailureStatus = ResultStatus.BadRequest,
        CancellationToken cancellationToken = default)
        where TDto : class
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            try
            {
                var dto = content.FromJson<TDto>();

                if (dto is null)
                {
                    return new Failure<TResponse>(
                        defaultResponse,
                        ResultStatus.BadRequest,
                        [new ValidationFailure(entityName, $"Unable to deserialize {entityName.ToLowerInvariant()} response")]);
                }

                return new Success<TResponse>(mapToResponse(dto));
            }
            catch (System.Text.Json.JsonException)
            {
                return new Failure<TResponse>(
                    defaultResponse,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(entityName, $"Unable to deserialize {entityName.ToLowerInvariant()} response")]);
            }
        }

        Failure<TResponse>? failure = null;
        try
        {
            failure = content.FromJson<Failure<TResponse>>();
        }
        catch (System.Text.Json.JsonException)
        {
            // Response body is not valid JSON, use defaults
        }

        var status = failure?.Status ?? MapHttpStatusCode(response.StatusCode, defaultFailureStatus);
        var errors = failure?.Errors ?? [new ValidationFailure(entityName, $"Unable to retrieve {entityName.ToLowerInvariant()}")];

        return new Failure<TResponse>(defaultResponse, status, errors);
    }

    private static ResultStatus MapHttpStatusCode(HttpStatusCode httpStatusCode, ResultStatus defaultStatus)
    {
        return httpStatusCode switch
        {
            HttpStatusCode.NotFound => ResultStatus.NotFound,
            HttpStatusCode.Unauthorized => ResultStatus.Unauthorized,
            HttpStatusCode.Forbidden => ResultStatus.Forbid,
            HttpStatusCode.BadRequest => ResultStatus.BadRequest,
            HttpStatusCode.UnprocessableEntity => ResultStatus.BadRequest,
            >= HttpStatusCode.InternalServerError => ResultStatus.Error,
            _ => defaultStatus
        };
    }

    /// <summary>
    /// Performs an HTTP GET request where the response body deserializes directly to the response type.
    /// </summary>
    protected async Task<Result<TResponse>> GetAsync<TResponse>(
        string url,
        TResponse defaultResponse,
        string entityName = "Response",
        ResultStatus defaultFailureStatus = ResultStatus.BadRequest,
        CancellationToken cancellationToken = default)
        where TResponse : class
    {
        return await GetAsync<TResponse, TResponse>(
            url,
            dto => dto,
            defaultResponse,
            entityName,
            defaultFailureStatus,
            cancellationToken);
    }

    /// <summary>
    /// Performs an HTTP GET request, throws on HTTP error, and returns deserialized response or default value.
    /// Use this for clients that throw exceptions rather than returning Result types.
    /// </summary>
    /// <typeparam name="T">The type to deserialize from the response body</typeparam>
    /// <param name="url">The URL to request</param>
    /// <param name="defaultValue">Default value to return if deserialization returns null</param>
    /// <param name="cancellationToken">Cancellation token</param>
    protected async Task<T> GetOrDefaultAsync<T>(
        string url,
        T defaultValue,
        CancellationToken cancellationToken = default)
        where T : class
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = content.FromJson<T>();
        return result ?? defaultValue;
    }

    /// <summary>
    /// Performs an HTTP GET request, throws on HTTP error, and maps the deserialized response.
    /// Use this for clients that throw exceptions rather than returning Result types.
    /// </summary>
    protected async Task<TResult> GetOrDefaultAsync<TResult, TDto>(
        string url,
        Func<TDto?, TResult> mapToResult,
        CancellationToken cancellationToken = default)
        where TDto : class
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var dto = content.FromJson<TDto>();
        return mapToResult(dto);
    }

    /// <summary>
    /// Performs an HTTP GET request, throws on HTTP error, and returns the raw string content.
    /// Use this for endpoints that return non-JSON content.
    /// </summary>
    protected async Task<string> GetStringAsync(
        string url,
        CancellationToken cancellationToken = default)
    {
        using var response = await HttpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    /// <summary>
    /// Performs an HTTP POST request with JSON body, throws on HTTP error, and returns deserialized response or default value.
    /// Use this for clients that throw exceptions rather than returning Result types.
    /// </summary>
    protected async Task<T> PostOrDefaultAsync<T, TRequest>(
        string url,
        TRequest request,
        T defaultValue,
        CancellationToken cancellationToken = default)
        where T : class
        where TRequest : class
    {
        var content = new StringContent(request.ToJson(), Encoding.UTF8, "application/json");
        using var response = await HttpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        return responseContent.FromJson<T>() ?? defaultValue;
    }

    /// <summary>
    /// Performs an HTTP POST request with JSON body, throws on HTTP error.
    /// Use this for fire-and-forget POST operations.
    /// </summary>
    protected async Task PostAsync<TRequest>(
        string url,
        TRequest request,
        CancellationToken cancellationToken = default)
        where TRequest : class
    {
        var content = new StringContent(request.ToJson(), Encoding.UTF8, "application/json");
        using var response = await HttpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public string GetProviderName() =>
        HttpClient?.BaseAddress?.Host ?? "Unknown";

    public async Task<Dictionary<string, object>> GetHealthStatus()
    {
        var hostName = string.Empty;

        try
        {
            var response = await HttpClient.GetAsync("health");
            response.EnsureSuccessStatusCode();

            if (response.Headers.TryGetValues("host", out var values))
            {
                hostName = values.First();
            }

            return new Dictionary<string, object>
            {
                { "status", response.StatusCode },
                { "uri", $"{HttpClient.BaseAddress}health" },
                { "host", hostName }
            };
        }
        catch (Exception ex)
        {
            return new Dictionary<string, object>
            {
                { "status", HttpStatusCode.ServiceUnavailable },
                { "uri", $"{HttpClient.BaseAddress}health" },
                { "host", hostName },
                { "ex", ex.Message }
            };
        }
    }
}