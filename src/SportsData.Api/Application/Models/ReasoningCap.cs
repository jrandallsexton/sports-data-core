using FluentValidation.Results;

using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Infrastructure.Clients.AI;

namespace SportsData.Api.Application.Models;

/// <summary>
/// A model's reasoning cap: at most one of an effort level or a token
/// budget (CK_Model_ReasoningEffortOrMaxTokens), and only on the OpenRouter
/// route — the one client that sends it. A cap on a direct row would be
/// stored and silently ignored.
/// </summary>
internal static class ReasoningCap
{
    public const string PropertyName = nameof(Model.ReasoningEffort);

    /// <summary>Null when valid; otherwise the failure to return as a 400.</summary>
    public static ValidationFailure? Validate(ModelGateway gateway, ModelReasoningEffort? effort, int? maxTokens)
    {
        if (effort is null && maxTokens is null)
        {
            return null;
        }

        if (gateway != ModelGateway.OpenRouter)
        {
            return new ValidationFailure(PropertyName, "A reasoning cap applies only to models routed via OpenRouter.");
        }

        if (effort is not null && maxTokens is not null)
        {
            return new ValidationFailure(PropertyName, "Set a reasoning effort OR a reasoning token budget, not both — OpenRouter accepts one.");
        }

        if (effort is not null && !Enum.IsDefined(effort.Value))
        {
            return new ValidationFailure(PropertyName, $"Unknown reasoning effort '{effort}'.");
        }

        if (maxTokens is < Model.MinReasoningMaxTokens or > Model.MaxReasoningMaxTokens)
        {
            return new ValidationFailure(
                nameof(Model.ReasoningMaxTokens),
                $"Reasoning token budget must be between {Model.MinReasoningMaxTokens} and {Model.MaxReasoningMaxTokens}.");
        }

        return null;
    }

    /// <summary>The request parameter for a catalog row; null = send none (model default).</summary>
    public static OpenRouterReasoning? ToOpenRouter(Model model)
    {
        if (model.ReasoningEffort is { } effort)
        {
            return new OpenRouterReasoning
            {
                Effort = effort == ModelReasoningEffort.Off ? "none" : effort.ToString().ToLowerInvariant()
            };
        }

        return model.ReasoningMaxTokens is { } maxTokens
            ? new OpenRouterReasoning { MaxTokens = maxTokens }
            : null;
    }
}
