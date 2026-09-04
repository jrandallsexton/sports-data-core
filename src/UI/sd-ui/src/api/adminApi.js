import apiClient from './apiClient';

const AdminApi = {
  getCompetitionsWithoutCompetitors: () =>
    // Controller likely under /ui/admin/errors/..., use /ui/admin as a safe prefix
    apiClient.get('/admin/errors/competitions-without-competitors'),
  getCompetitionsWithoutPlays: () =>
    apiClient.get('/admin/errors/competitions-without-plays'),
  getCompetitionsWithoutDrives: () =>
    apiClient.get('/admin/errors/competitions-without-drives'),
  getCompetitionsWithoutMetrics: () =>
    apiClient.get('/admin/errors/competitions-without-metrics'),
  // sport: backend Sport enum name (e.g. "FootballNfl"); omitted = NCAA.
  // contestId is encoded everywhere it enters the path — it comes from
  // free-text admin inputs, and a stray ?/#// would change the request target.
  resetPreview: (contestId, sport) =>
    apiClient.post(
      `/admin/matchup/preview/${encodeURIComponent(contestId)}/reset${sport ? `?sport=${encodeURIComponent(sport)}` : ""}`
    ),

  // Preview Lab (docs/metrics-modeling/matchup-preview-data-inputs.md §3.6).
  // capture = persist the exact prompt payload without a model call;
  // experiment = call the model but store the result on the capture row
  // ONLY — never writes a MatchupPreview, so a prior season's real preview
  // can't be shadowed on the picks page. Both allow completed contests and
  // announce completion via SignalR (PreviewPromptCaptured).
  // promptId (optional): Prompt entity GUID selecting a specific prompt
  // version from the API database for this run. An unknown id fails the
  // run loudly rather than silently using the slot default.
  capturePreviewPrompt: (contestId, sport, promptId) =>
    apiClient.post(
      `/admin/matchup/preview/${encodeURIComponent(contestId)}/capture`,
      null,
      { params: { ...(sport ? { sport } : {}), ...(promptId ? { promptId } : {}) } }
    ),
  // modelId (optional): run against that Model row instead of the
  // production client — the Model Lab's single-cell fill-in.
  runPreviewExperiment: (contestId, sport, promptId, modelId) =>
    apiClient.post(
      `/admin/matchup/preview/${encodeURIComponent(contestId)}/experiment`,
      null,
      {
        params: {
          ...(sport ? { sport } : {}),
          ...(promptId ? { promptId } : {}),
          ...(modelId ? { modelId } : {}),
        },
      }
    ),
  getPreviewCaptures: (contestId) =>
    apiClient.get(`/admin/matchup/preview/${encodeURIComponent(contestId)}/captures`),
  // Model Consensus Lab fan-out: one Experiment per active, lab-reachable
  // model (same prompt, same contest) — a capture row per model, never a
  // MatchupPreview. docs/features/model-consensus-lab.md.
  runPreviewPanel: (contestId, sport, promptId) =>
    apiClient.post(
      `/admin/matchup/preview/${encodeURIComponent(contestId)}/experiment/panel`,
      null,
      { params: { ...(sport ? { sport } : {}), ...(promptId ? { promptId } : {}) } }
    ),
  // Week matrix: contests any pick'em league carries for (sport, year,
  // week) x active lab-reachable models, latest picks per pair.
  // promptId scopes the matrix to experiments generated with that prompt
  // (the corpus is payload x model x prompt); omitted = latest run
  // regardless of prompt (mixed view).
  getModelLabMatrix: (sport, seasonYear, week, promptId) =>
    apiClient.get('/admin/model-lab/matrix', {
      params: { sport, seasonYear, week, ...(promptId ? { promptId } : {}) },
    }),

  // Prompt management (per-sport-league prompt entities; text lives in
  // the API database). Name and slot (sport, withStats) are immutable —
  // a different slot means creating a new version.
  getPrompts: () => apiClient.get('/admin/prompts'),
  getPrompt: (promptId) => apiClient.get(`/admin/prompts/${encodeURIComponent(promptId)}`),
  createPrompt: (body) => apiClient.post('/admin/prompts', body),
  updatePrompt: (promptId, body) =>
    apiClient.put(`/admin/prompts/${encodeURIComponent(promptId)}`, body),
  setDefaultPrompt: (promptId) =>
    apiClient.post(`/admin/prompts/${encodeURIComponent(promptId)}/set-default`),
  importPromptFromBlob: (body) => apiClient.post('/admin/prompts/import-blob', body),

  // Model management (provider fleets + model identity records driving
  // the experiment harness and production routing; seed data in
  // docs/metrics-modeling/llm-training-dates.md). Identity fields are
  // immutable after creation; set-default flips THE production model.
  getModelProviders: () => apiClient.get('/admin/model-providers'),
  createModelProvider: (body) => apiClient.post('/admin/model-providers', body),
  getModels: () => apiClient.get('/admin/models'),
  getModel: (modelId) => apiClient.get(`/admin/models/${encodeURIComponent(modelId)}`),
  createModel: (body) => apiClient.post('/admin/models', body),
  updateModel: (modelId, body) =>
    apiClient.put(`/admin/models/${encodeURIComponent(modelId)}`, body),
  setDefaultModel: (modelId) =>
    apiClient.post(`/admin/models/${encodeURIComponent(modelId)}/set-default`),

  // Returns one MLB matchup in the same shape as the picks page so the
  // baseball SignalR debug page can render a real <MatchupCard /> for a
  // chosen contest. League-context fields (Predictions, AiWinner,
  // IsPreview*, HeadLine) come back null/empty per the endpoint contract.
  getBaseballMatchupForContest: (contestId) =>
    apiClient.get(`/admin/baseball/contests/${contestId}/matchup`),
  getFootballMatchupForContest: (contestId, league) =>
    apiClient.get(`/admin/football/contests/${contestId}/matchup`, {
      params: league ? { league } : undefined,
    }),

  // Triggers a contest replay through the matching sport's Producer.
  // Producer enqueues the work and the bus emits ContestStatusChanged
  // once + a sport-specific *PlayCompleted per stored play. Use this
  // alongside the matchup card observer / debug card to verify the
  // SignalR pipeline end-to-end against a real game.
  replayBaseballContest: (contestId) =>
    apiClient.post(`/admin/baseball/contests/${contestId}/replay`),
  replayFootballContest: (contestId, league) =>
    apiClient.post(`/admin/football/contests/${contestId}/replay`, null, {
      params: league ? { league } : undefined,
    }),

  // Re-run enrichment for a single contest. Clears UserPick scoring
  // fields and asks Producer to clear the Contest derived fields and
  // re-run enrichment inline. Response.data is the CorrelationId
  // Producer logged the work under — surfaced in ContestOverviewAdmin
  // so an operator can paste it into Seq for tracing.
  //
  // Lives here (AdminApi → /admin/...) deliberately. The earlier
  // sibling actions on contestApi (Refresh / Refresh Media / Finalize)
  // are exposed under the general /ui/contest surface gated only by
  // [Authorize]. Re-enrich rolls back UserPicks, so it MUST be on the
  // admin-token-gated controller — the UI's isAdmin check is
  // presentational only, not a trust boundary.
  reenrichContest: (contestId, sport, league) =>
    apiClient.post(`/admin/contest/${contestId}/reenrich`, null, {
      params: { sport, league }
    }),

  // SmackBot Lab (docs/features/smackbot-lab.md). API composes pick facts
  // and relays preview/phrases/ratings to Notification through its typed
  // client — the browser never holds Notification's key, only the admin
  // token this whole surface already requires.
  getSmackLabLeagues: () => apiClient.get('/admin/smack-lab/leagues'),
  getSmackLabPicks: (leagueId) =>
    apiClient.get(`/admin/smack-lab/leagues/${encodeURIComponent(leagueId)}/picks`),
  smackLabPreview: (body) => apiClient.post('/admin/smack-lab/preview', body),
  getSmackLabRatings: (leagueId) =>
    apiClient.get(`/admin/smack-lab/leagues/${encodeURIComponent(leagueId)}/ratings`),
  getSmackPhrases: () => apiClient.get('/admin/smack-lab/phrases'),
  createSmackPhrase: (body) => apiClient.post('/admin/smack-lab/phrases', body),
  updateSmackPhrase: (phraseId, body) =>
    apiClient.put(`/admin/smack-lab/phrases/${encodeURIComponent(phraseId)}`, body),
  rateSmackPreview: (body) => apiClient.post('/admin/smack-lab/ratings', body),

  // SignalR debug harness — see docs/signalr-debug-harness-plan.md.
  // Each call publishes a synthetic integration event through API's
  // MassTransit + own consumer + SignalR fan-out, exercising the same
  // path a real Producer-originated event would. Server stamps the
  // ContestId so the client can't fan out for a real contest.
  // *PlayCompleted carries play description + scoreboard tick in one
  // event — there is no longer a separate play-completed broadcast.
  broadcastContestStatus: (payload) =>
    apiClient.post('/admin/signalr-debug/contest-status', payload),
  broadcastFootballPlay: (payload) =>
    apiClient.post('/admin/signalr-debug/football-play', payload),
  broadcastBaseballPlay: (payload) =>
    apiClient.post('/admin/signalr-debug/baseball-play', payload),
};

export default AdminApi;
