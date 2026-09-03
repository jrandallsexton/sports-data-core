# Model Consensus Lab — design

**Status:** authorized 2026-09-03 (discussion in-session); Phase 1 built

**Decision amendment (2026-09-03):** the 2026-08-08 model-strategy decision
(docs/metrics-modeling/matchup-preview-data-inputs.md §6) chose first-party
clients only and passed on OpenRouter. That call predated the many-model
audition ambition; the owner revised it. Taxonomy matters (owner's
correction): OpenRouter is NOT a provider — it is a gateway we communicate
through. Providers stay who they are (Anthropic makes Claude however we
reach it); the route lives on the Model row as `Gateway` (None |
OpenRouter). First-party clients remain preferred for production panel
seats (IP path, exact identity, SPOF isolation).
**Owner intent:** run the exact same prompt against every model we can reach,
score picks against actuals in a matrix, and — once the evidence supports it —
make StatBot's visible prediction the CONSENSUS of a small panel (minimum 2,
realistically 3 models). Models get added and discarded over time; the
methodology does not change. Admin-only throughout.

## Two phases of life, two transports

**Audition (breadth):** every candidate model runs through **OpenRouter** —
one account, one OpenAI-compatible API, every provider's models. Its entire
value is exactly this use case: same prompt, many models, no per-provider
signups, one bill. Acceptable trust surface for admin-only experiments
(prompts already transit DeepSeek today). Routing markup is noise at
experiment scale.

**Production panel (the chosen ~3): direct per-provider clients.** Two
reasons this is not optional:
1. **No shared single point of failure** — a panel routed through one
   aggregator loses ALL panelists in one outage; direct clients fail
   independently, and 2-of-3 still resolves with one provider down.
2. Native structured-output modes, no markup, keys in cluster secrets where
   everything else lives. The `IProvideAiCommunication` abstraction already
   exists (DeepSeek, Ollama); each new provider is a day of work behind it.

**Promotion rule:** before a model graduates from audition to panel,
re-validate it through the direct client — aggregator routing can differ
subtly (sampling defaults, host selection for open models). The panel is
scored on exactly what production will call.

## Architecture

- **Model registry**: the EXISTING `Model` + `ModelProvider` entities
  (designed 2026-08-08 for exactly this work; admin CRUD already lives at
  the `models` / `model-providers` routes). No new catalog table — the
  first Phase 1 build invented a duplicate `AiModelCatalogEntry` and was
  reworked 2026-09-03 when the owner caught it. `Model` gained `Gateway`
  (None | OpenRouter — a routing attribute, not a provider): a routed row
  keeps its TRUE provider (Anthropic makes Claude however we reach it) and
  carries the gateway's namespaced ApiModelId (exact ids only — provider
  aliases silently swap weights mid-season). The same weights reached
  directly vs via the gateway are DIFFERENT evaluands — two rows under the
  same provider, kept apart by (ModelProviderId, ApiModelId) since gateway
  ids are namespaced. Gateway is identity (create-only, like ApiModelId).
  Bonus the duplicate lacked: `KnowledgeCutoffUtc` +
  evidence lets the scoring harness classify each run's contamination risk,
  and cost-per-MTok rides on the row.
- **Evaluation run**: NOT a new table — `MatchupPreviewPrompt` already
  charters itself as "the backtest corpus (payload x model x prompt vs
  actual outcome)" and its Experiment mode already stores the exact prompt
  + raw response without ever writing a MatchupPreview. Phase 1 extends it
  with the queryable measurements: ModelId (no FK — the Model string stays
  provenance, same pattern as PromptId), parsed SU/ATS picks, actual
  prompt/completion tokens, latency. Parsed picks persist even when
  validation flags problems — the matrix scores the pick; the problems
  column records the caveat.
- **Fan-out runner**: admin-triggered (Hangfire job), one evaluation per
  active Model under an active, lab-reachable provider. Parse failure /
  refusal / timeout is a first-class outcome ("abstained") — never
  retry-until-it-parses.
- **Client factory**: resolves a Model row to an evaluation client — the
  row's `Gateway` picks the route (OpenRouterClient with the row's
  ApiModelId today); for direct routes the provider's `Kind` picks the
  first-party implementation as panel seats are earned.
  `CanResolve(gateway, kind)` is the single source of truth for
  reachability — an unreachable route is a logged skip, never a Hangfire
  retry loop. Existing preview pipeline untouched.

## The matrix (admin UI, Phase 2)

Rows = models; per contest: SU pick / ATS pick cells; actuals fill in on
finalization; running SU% / ATS% accuracy columns accumulate. **Baseline
rows keep everyone honest:** always-home, always-favorite (spread), and
deetsMeter (the statistical model already predicts these games).

The honest caution the baselines exist for: LLMs share training data and
biases and mostly lean chalk — a consensus of N models may converge on
"pick the favorite." The matrix's first job is to prove or DISPROVE that
any model or ensemble beats the naive baselines and our own statistical
model. Cheap disproof before shipping is a success outcome.

## Consensus resolver (Phase 4, dark until evidence)

Panel of 3 (odd -> simple majority cannot tie on a binary pick). Residual
mechanics: an abstention leaves 2 that can split — **deetsMeter breaks the
tie** (statistics settle the LLM disagreement; pending owner confirmation).
The resolver is VERSIONED like modelVersion, so every historical StatBot
pick stays attributable to the rule that made it. Runs dark beside the
live pick until weeks of season data justify the swap.

## Phasing

1. **Registry + OpenRouter client + fan-out + persistence** — BUILT
   2026-09-03 (reworked same day onto the existing Model/ModelProvider
   entities): Model.Gateway (None | OpenRouter), OpenRouterClient
   (IProvideModelEvaluation: content + tokens + latency; temperature 0.1 —
   the lab compares models, sampling noise muddies that), gateway-keyed
   client resolver (direct routes deliberately unreachable until a panel
   seat is earned), model gate at the TOP of MatchupPreviewProcessor (an
   inactive model costs nothing — not even a Producer round trip), and
   POST admin/matchup/preview/{contestId}/experiment/panel fanning out one
   Experiment per active, lab-reachable model (25-model budget guard).
   AppConfig keys: CommonConfig:OpenRouterClientConfig:{ApiKey,BaseUrl}.
   Seed roster: sql/pgsql/seed_model_lab.sql (idempotent; ids verified
   against OpenRouter's live catalog 2026-09-03; cutoffs transcribed from
   llm-training-dates.md UNVERIFIED — the Model Manager's evidence/verified
   fields are the follow-through). ModelProviderKind.GatewayOnly (99)
   covers long-tail makers with no first-party client (xAI et al.).
2. Matrix UI in the admin area + scoring-on-finalization + baselines.
3. Season data accumulates; audition decides the panel; direct clients for
   the chosen 3.
4. Consensus resolver behind a flag, dark comparison, swap on evidence.

## Open questions (owner)

- Baseline rows in the matrix — confirmed in?
- deetsMeter as the abstention tie-break — confirmed?
- Once stable: auto-run the audition weekly for league-backing contests, or
  keep admin-triggered?

## Constraints carried forward

- Prompts NEVER in source control (repo is public); the harness stores
  prompt REFERENCES to existing prompt entities only.
- A per-run budget cap even though experiment spend is approved — a runaway
  loop across N paid APIs deserves a ceiling, not trust.
- Admin-only surface end to end until the Phase 4 swap.
