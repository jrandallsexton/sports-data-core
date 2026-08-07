namespace SportsData.Api.Application.Previews;

public enum PreviewGenerationMode
{
    /// <summary>
    /// Production path: call the model and persist a MatchupPreview.
    /// Skips completed contests. Default — Hangfire payloads serialized
    /// before this enum existed deserialize to this value.
    /// </summary>
    Generate = 0,

    /// <summary>
    /// Dry run: assemble and persist the prompt payload only. No model
    /// call, no MatchupPreview. Completed contests allowed.
    /// </summary>
    Capture = 1,

    /// <summary>
    /// Eval run: assemble, persist the payload, call the model, and store
    /// the raw response on the capture row. NEVER writes a MatchupPreview —
    /// the picks page reads newest-non-rejected per contest, so an
    /// experimental preview row would shadow last season's real preview.
    /// Completed contests allowed (that is the point).
    /// </summary>
    Experiment = 2
}
