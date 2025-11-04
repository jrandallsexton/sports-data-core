using System.Text.Json.Serialization;

namespace SportsData.Api.Application.Previews.Commands;

public class ApproveMatchupPreviewCommand
{
    [JsonPropertyName("previewId")]
    public Guid PreviewId { get; set; }

    public Guid ApprovedByUserId { get; set; }
}