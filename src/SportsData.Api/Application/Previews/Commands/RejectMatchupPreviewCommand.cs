using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Previews.Commands;

public class RejectMatchupPreviewCommand
{
    [JsonPropertyName("previewId")]
    public Guid PreviewId { get; set; }

    [JsonPropertyName("contestId")]
    public Guid ContestId { get; set; }

    [JsonPropertyName("rejectionNote")]
    public required string RejectionNote { get; set; }

    public Guid RejectedByUserId { get; set; }
}