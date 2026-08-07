using SportsData.Core.Common;

namespace SportsData.Api.Application.Previews;

public class GenerateMatchupPreviewsCommand
{
    public Guid ContestId { get; set; }

    /// <summary>
    /// Which sport's canonical clients resolve this contest. Defaults to
    /// FootballNcaa so any payload serialized before this property existed
    /// (or a caller that omits it) behaves exactly as before.
    /// </summary>
    public Sport Sport { get; set; } = Sport.FootballNcaa;

    /// <summary>
    /// Generate (default, prod), Capture (prompt only, no model call), or
    /// Experiment (model call, result stored on the capture row, no
    /// MatchupPreview written). Defaults to Generate so payloads serialized
    /// before this property existed behave exactly as before.
    /// </summary>
    public PreviewGenerationMode Mode { get; set; } = PreviewGenerationMode.Generate;

    public Guid CorrelationId { get; set; } = Guid.NewGuid();
}