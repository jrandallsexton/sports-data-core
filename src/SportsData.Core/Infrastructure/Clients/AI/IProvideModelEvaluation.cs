using SportsData.Core.Common;

using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Infrastructure.Clients.AI;

/// <summary>
/// Evaluation-grade AI call: like <see cref="IProvideAiCommunication"/> but
/// returns the measurement the Model Consensus Lab scores on — token usage
/// and latency — instead of only logging it. Implemented by clients that
/// participate in model evaluation (OpenRouter for the audition; direct
/// provider clients as panel seats are earned).
/// See docs/features/model-consensus-lab.md.
/// </summary>
public interface IProvideModelEvaluation
{
    Task<Result<AiEvaluationResult>> EvaluateAsync(
        string prompt,
        CancellationToken ct = default);

    string GetModelName();
}

/// <summary>One model call's outcome, with the measurements the lab persists.</summary>
/// <param name="FinishReason">
/// Why generation stopped, as the transport reported it ("stop" = natural
/// end; "length" = TRUNCATED at the max-tokens ceiling — the content is
/// partial and any parse failure is a config problem, not a model problem).
/// Null when the transport did not say.
/// </param>
public sealed record AiEvaluationResult(
    string Content,
    int? PromptTokens,
    int? CompletionTokens,
    long LatencyMs,
    string? FinishReason = null);
