using System.Text.Json.Serialization;

using SportsData.Core.Common;

namespace SportsData.Api.Application.Previews.Commands;

public class RejectMatchupPreviewCommand
{
    [JsonPropertyName("previewId")]
    public Guid PreviewId { get; set; }

    /// <summary>Sport for the regeneration enqueued after rejection.
    /// Defaults to NCAA for callers that omit it.</summary>
    [JsonPropertyName("sport")]
    public Sport Sport { get; set; } = Sport.FootballNcaa;

    [JsonPropertyName("contestId")]
    public Guid ContestId { get; set; }

    [JsonPropertyName("rejectionNote")]
    public required string RejectionNote { get; set; }

    public Guid RejectedByUserId { get; set; }
}