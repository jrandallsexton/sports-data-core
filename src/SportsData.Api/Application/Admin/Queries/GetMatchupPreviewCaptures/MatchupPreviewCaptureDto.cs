using SportsData.Api.Application.Previews;
using SportsData.Core.Common;

namespace SportsData.Api.Application.Admin.Queries.GetMatchupPreviewCaptures;

public class MatchupPreviewCaptureDto
{
    public Guid Id { get; set; }

    public Guid ContestId { get; set; }

    public Sport Sport { get; set; }

    /// <summary>Null for dry-run captures; set when a real generation produced a preview.</summary>
    public Guid? MatchupPreviewId { get; set; }

    public string PromptVersion { get; set; } = default!;

    /// <summary>The serialized matchup DTO — the data part of the prompt.</summary>
    public string PayloadJson { get; set; } = default!;

    public string? EditorNote { get; set; }

    /// <summary>
    /// Exactly what the model would receive: instruction blob + payload +
    /// editor note, reconstructed the same way the processor renders it.
    /// </summary>
    public string FullPrompt { get; set; } = default!;

    public int CharCount { get; set; }

    public int EstTokens { get; set; }

    public PreviewGenerationMode Mode { get; set; }

    /// <summary>Model name for runs that called the model (Generate/Experiment).</summary>
    public string? Model { get; set; }

    /// <summary>Raw model response (Generate/Experiment); may be malformed — that's the data.</summary>
    public string? RawResponse { get; set; }

    /// <summary>Parse/validation problems recorded on experiment runs; null = clean.</summary>
    public string? ResponseValidationErrors { get; set; }

    // Model Consensus Lab measurements (experiment runs; null elsewhere).
    // See docs/features/model-consensus-lab.md.

    /// <summary>The Model entity that supplied the evaluation (Model string is the provenance of record).</summary>
    public Guid? ModelId { get; set; }

    /// <summary>Parsed SU pick (FranchiseSeasonId); null when the response did not parse.</summary>
    public Guid? PredictedStraightUpWinnerId { get; set; }

    /// <summary>Parsed ATS pick (FranchiseSeasonId); null when absent or unparsed.</summary>
    public Guid? PredictedSpreadWinnerId { get; set; }

    /// <summary>Actual prompt tokens reported by the transport (EstTokens is the pre-call estimate).</summary>
    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    public long? LatencyMs { get; set; }

    public DateTime CreatedUtc { get; set; }
}
