using Microsoft.Extensions.Logging;

using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.AI;

using System;
using System.Net.Http;

namespace SportsData.Api.Application.Previews;

/// <summary>
/// Resolves a <see cref="Model"/> row (with its <see cref="ModelProvider"/>
/// loaded) to a callable evaluation client: the row's
/// <see cref="Model.Gateway"/> picks the route, and for direct routes the
/// provider's <see cref="ModelProvider.Kind"/> picks the first-party
/// implementation. See docs/features/model-consensus-lab.md.
/// </summary>
public interface IAiModelClientResolver
{
    /// <summary>True when <see cref="Resolve"/> has a client for this (gateway, kind) pair.</summary>
    bool CanResolve(ModelGateway gateway, ModelProviderKind kind);

    IProvideModelEvaluation Resolve(Model model);
}

/// <inheritdoc />
public sealed class AiModelClientResolver : IAiModelClientResolver
{
    /// <summary>Named HttpClient registered in Program.cs (timeout etc.).</summary>
    public const string OpenRouterHttpClientName = "openrouter";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenRouterClientConfig _openRouterConfig;
    private readonly ILoggerFactory _loggerFactory;

    public AiModelClientResolver(
        IHttpClientFactory httpClientFactory,
        OpenRouterClientConfig openRouterConfig,
        ILoggerFactory loggerFactory)
    {
        _httpClientFactory = httpClientFactory;
        _openRouterConfig = openRouterConfig;
        _loggerFactory = loggerFactory;
    }

    // Direct (Gateway=None) evaluation clients arrive with panel promotion
    // (phase 3 of the design doc) — a model earns its first-party client
    // through the OpenRouter audition. Until then only gateway-routed rows
    // are reachable, whichever provider made them.
    public bool CanResolve(ModelGateway gateway, ModelProviderKind kind) =>
        gateway == ModelGateway.OpenRouter;

    public IProvideModelEvaluation Resolve(Model model)
    {
        if (model.ModelProvider is null)
        {
            throw new InvalidOperationException(
                $"Model {model.Id} was loaded without its ModelProvider — the resolver needs Kind. Include it in the query.");
        }

        return model.Gateway switch
        {
            ModelGateway.OpenRouter => new OpenRouterClient(
                _httpClientFactory.CreateClient(OpenRouterHttpClientName),
                _openRouterConfig,
                model.ApiModelId,
                _loggerFactory.CreateLogger<OpenRouterClient>()),

            _ => throw new NotSupportedException(
                $"No lab evaluation client for gateway {model.Gateway} / provider kind " +
                $"{model.ModelProvider.Kind} (model {model.Name}). See docs/features/model-consensus-lab.md.")
        };
    }
}
