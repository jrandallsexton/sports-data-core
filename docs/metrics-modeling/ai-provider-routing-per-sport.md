# Per-Sport AI Provider Routing — DeepSeek for paid tiers, Ollama for everything else

**Status:** design / not yet implemented
**Owners:** Randall
**Target test bed:** MLB game recaps and matchup previews; NCAAFB Division II as a near-term secondary use

## TL;DR

This is **not a cutover**. DeepSeek stays as the production AI provider for the tiers where editorial quality matters and users care: **NCAAFB FBS and NFL**. Ollama on Bender lights up for everything else — **MLB** first (plumbing-test), and **NCAAFB Division II** (and any other lower-tier NCAA divisions) as a follow-on.

The routing dimension is **(Sport, Division)**, not just Sport. NCAAFB FBS and NCAAFB Division II are the same `Sport.FootballNcaa` enum value — division lives lower in the data model (on the conference's `Division` string, e.g. `"FBS (I-A)"`). So the factory has to look one layer deeper than the sport enum to pick the right provider.

The work:

1. A `(Sport, Division)`-aware factory in front of `IProvideAiCommunication` (mirrors `IFranchiseClientFactory.Resolve(sport)` but with a second dimension)
2. AppConfig that maps `(Sport, Division)` → provider, with a per-sport `Default` fallback
3. `OllamaClient.cs` already exists and is registered; what's missing is the factory + the per-sport-and-division config
4. The two `TODO: multi-sport` markers in `MatchupPreviewProcessor` close first — MLB and Division II previews don't work today regardless of provider

Estimated effort: 1–2 weeks for routing; multi-sport prep is its own preceding PR.

---

## What "division" means here

NCAAFB is the only sport in the current roster that subdivides for routing purposes:

- **FBS (I-A)** — Power 4 + Group of 5. Paid DeepSeek.
- **FCS (I-AA)** — playoff division. Open question; treat as "lower-tier" until specified otherwise.
- **Division II** — Ollama.
- **Division III** — Ollama (and NAIA/NJCAA if/when they're sourced).

NFL has no relevant subdivision — it's all NFL. MLB has no relevant subdivision for AI purposes (MLB vs MiLB isn't currently a concern). Future sports may or may not subdivide; the routing shape has to handle "no division" cleanly.

The division lives on the conference's `Division` string field (see `LeagueCreatePage.jsx:254`: `c.division === "FBS (I-A)"` and the FBS-anchored SQL queries under `src/SportsData.Api/Infrastructure/Data/Canonical/Sql/Errors/`). `MatchupPreviewProcessor` already touches the contest's franchise data and so already has the path to discover the division — it doesn't need a new lookup, just a deeper read of what it already loads.

---

## Current state

### The flow (provider-agnostic by design)

End-to-end for matchup previews:

1. Admin POSTs `/admin/matchup/preview/{contestId}/reset` (`AdminController.cs:93`)
2. Enqueues `GenerateMatchupPreviewsCommand` to Hangfire
3. `MatchupPreviewProcessor.Process(command)` runs:
   - Resolves contest via `ContestClient`
   - Fetches franchise stats + metrics
   - Loads prompt via `MatchupPreviewPromptProvider` (lazy-cached from Azure Blob)
   - Calls `_aiCommunication.GetResponseAsync(fullPrompt)` ← **this is the single seam**
   - Parses response as `MatchupPreviewResponse` (V1 flat) with `MatchupPreviewResponseV2` (nested) fallback
   - Runs semantic validation (score coherence, winner GUIDs, spread math)
   - Inserts `MatchupPreview` row, stamps `Model` = `_aiCommunication.GetModelName()` and `PromptVersion` = prompt blob name
   - Publishes `PreviewGenerated`
4. `PreviewGeneratedHandler` consumes the event and SignalR-broadcasts to clients
5. Admin approves or rejects. Rejection re-enqueues with the rejection note injected into the next prompt iteration

Game recaps follow the same shape via `GenerateGameRecapCommandHandler`.

### What's already provider-agnostic

- `IProvideAiCommunication` — `GetResponseAsync`, `GetTypedResponseAsync<T>`, `GetModelName()`
- `DeepSeekClient` — bearer-token, OpenAI-compatible `/chat/completions`, global semaphore throttle
- `OllamaClient` — `/api/generate`, same shape, same interface

Both clients honor `CancellationToken`. Both stamp `GetModelName()` so any future model-explainer surface can attribute a preview to a specific model run.

### What's NOT wired

DI registration in `Program.cs`:

```csharp
// Lines ~184–196: Ollama config + HttpClient are registered…
services.AddHttpClient<OllamaClient>(...);

// Lines ~198–212: DeepSeek config + HttpClient are registered…
services.AddHttpClient<DeepSeekClient>(...);

// Line 212: …but only DeepSeek is bound to the interface globally.
services.AddScoped<IProvideAiCommunication>(sp => sp.GetRequiredService<DeepSeekClient>());
```

That global binding has to go. Replaced by an injectable factory.

### AppConfig keys today

DeepSeek (in use, global):

- `CommonConfig:DeepSeekClientConfig:ApiKey`
- `CommonConfig:DeepSeekClientConfig:Model` (e.g. `deepseek-chat`)
- `CommonConfig:DeepSeekClientConfig:BaseUrl`

Ollama (registered, unused):

- `CommonConfig:OllamaClientConfig:Model`
- `CommonConfig:OllamaClientConfig:BaseUrl`

### Existing documentation in this folder

The four relocated docs (`DeepSeek_GameRecap_Summary.md`, `GameRecapGeneration_Guide.md`, `QuickStart_GameRecap.md`, `LLMs.md`) cover the happy-path testing scenario for the DeepSeek flow. `LLMs.md` carries stale Azure-ACA assumptions. Background only, not the routing plan.

---

## Decisions to make

### Decision 1 — Factory signature

Mirror `IFranchiseClientFactory.Resolve(sport)`, but with a second dimension for the NCAAFB FBS/Division II split.

```csharp
public interface IAiClientFactory
{
    /// <summary>
    /// Resolves the AI client for a given (sport, division) pair.
    /// Division is nullable — sports that don't subdivide (NFL, MLB) pass null.
    /// </summary>
    IProvideAiCommunication Resolve(Sport sport, string? division = null);
}
```

The factory holds references to both `DeepSeekClient` and `OllamaClient` (registered as concrete types in DI, not via the interface), reads a `(Sport, Division)` → provider map at construction, and returns the right one.

Consumers change from:

```csharp
private readonly IProvideAiCommunication _aiCommunication;
// ...
await _aiCommunication.GetResponseAsync(prompt);
```

to:

```csharp
private readonly IAiClientFactory _aiClientFactory;
// ...
var division = ResolveDivisionFor(contest);  // existing data
var client = _aiClientFactory.Resolve(command.Sport, division);
await client.GetResponseAsync(prompt);
```

The `IProvideAiCommunication` direct binding in `Program.cs:212` goes away — nothing should inject the interface directly anymore. The factory is the only entry point.

### Decision 2 — AppConfig key shape

Sport-keyed at minimum, with an optional second segment for division. Per the existing project convention captured in memory: API (mode=All) needs sport in key name; single-sport services use non-keyed entries.

For the API:

```
CommonConfig:AiProviderRouting:FootballNcaa:FBS         = DeepSeek
CommonConfig:AiProviderRouting:FootballNcaa:Default     = Ollama
CommonConfig:AiProviderRouting:FootballNfl              = DeepSeek
CommonConfig:AiProviderRouting:BaseballMlb              = Ollama
```

Lookup rules:

1. Try `CommonConfig:AiProviderRouting:{Sport}:{Division}` if `division` is non-null
2. Fall back to `CommonConfig:AiProviderRouting:{Sport}:Default`
3. Fall back to `CommonConfig:AiProviderRouting:{Sport}` (single-key form for sports with no subdivisions)
4. If none match — **fail closed**. Don't silently default to either provider. Missing routing should surface immediately at startup or on first inference attempt.

This means:
- FBS games hit the explicit `FootballNcaa:FBS` key → DeepSeek
- Division II games hit `FootballNcaa:Default` → Ollama
- FCS games hit `FootballNcaa:Default` → Ollama (with the explicit understanding that if you want FCS on DeepSeek later, you add a `FootballNcaa:FCS` key)
- NFL games hit `FootballNfl` → DeepSeek
- MLB games hit `BaseballMlb` → Ollama

For single-sport services (Producer in MLB mode etc.): non-keyed `CommonConfig:AiProvider = Ollama`. Currently no single-sport service uses AI directly, but defining the convention now keeps the future symmetrical.

### Decision 3 — How is "Division" discovered in the processor?

`MatchupPreviewProcessor` already pulls the contest's franchise data. The conference attached to each franchise carries the `Division` string. So the processor *can* determine division without new lookups — but the exact mechanism (home team's division, both teams', a contest-level cached field) and the cross-division tiebreak (FBS plays FCS happens) are implementation details.

**Deferred.** Resolve during Phase 1. Several reasonable approaches exist; the factory signature in Decision 1 doesn't change based on which is picked.

### Decision 4 — Network path from K3s to Bender

`OllamaClient.cs:49` carries a stray `ngrok-skip-browser-warning` header — a relic of an earlier tunnel-based dev cycle. Production-permanent ngrok is wrong.

Options:

- **Direct LAN access.** If Bender is on the same physical network as the K3s nodes and has a stable LAN IP. AppConfig `BaseUrl` becomes something like `http://bender.lan:11434`. Zero new infrastructure.
- **Tailscale.** Cluster nodes join a tailnet with Bender. AppConfig `BaseUrl` becomes a magic-DNS hostname. Survives network changes. Adds one daemon to each K3s worker.
- **Reverse proxy through cluster ingress.** A Kubernetes Service of type `ExternalName` or NodePort exposed on Bender, fronted by Traefik. More moving parts than direct LAN if direct LAN works.

The ngrok header should be deleted regardless.

**Open question:** is Bender LAN-reachable from K3s nodes today, or do they sit on separate subnets?

**Recommendation:** direct LAN if reachable; Tailscale if not. Defer the cluster-ingress option.

### Decision 5 — Which Ollama model

MLB and lower-NCAAFB previews are quality-flexible by the user's own framing — but the JSON-emission contract still matters. If the model wraps the response in markdown or hallucinates extra fields, the parser fails closed and the row never lands.

#### Bender capacity (as built)

- CPU: Intel Core i9-12900K (16 cores / 24 threads, 8P+8E)
- GPU: NVIDIA GeForce RTX 3060 Ti — **8 GB VRAM** (this is the binding constraint)
- System RAM: 128 GB DDR4 @ 4000 MT/s

8 GB VRAM rules out every 70B-class model on GPU-only. The substantial system RAM opens up CPU-offload as a real option — Ollama can split a model between GPU layers and CPU layers, trading throughput for capacity.

#### Realistic candidates

| Model | Quant | VRAM-only footprint | Placement on Bender | Notes |
|---|---|---|---|---|
| Llama 3.1 8B Instruct | Q5_K_M | ~5.7 GB | Fully on GPU | Fast (~30+ tok/s); modest JSON-emission quality; reasonable baseline |
| Qwen 2.5 7B Instruct | Q5_K_M | ~5.5 GB | Fully on GPU | Slightly better structured-output reputation than Llama 8B at this size |
| Gemma 2 9B Instruct | Q4_K_M | ~5.8 GB | Fully on GPU | Worth trying as a third option |
| Qwen 2.5 14B Instruct | Q4_K_M | ~8.5 GB | Just barely doesn't fit GPU-only; needs ~2 GB CPU offload for KV cache | Better quality than the 7Bs; mild latency cost |
| Llama 3.3 70B Instruct | Q4_K_M | ~40 GB | Heavy CPU offload (~20 layers GPU, rest CPU) | Quality jump; **3–8 tokens/sec realistic** — 30–90s per preview. Acceptable for batch admin generation, not for any realtime path |
| Mixtral 8x7B Instruct | Q4_K_M | ~26 GB | Moderate CPU offload | Faster than dense 70B at similar quality; still ~10–15 tok/s with partial GPU |

#### Recommendation

Pilot two models side-by-side on a small fixture set:

1. **Qwen 2.5 7B Instruct (Q5_K_M)** — primary candidate for "fits comfortably on GPU, fast, good enough for plumbing-test."
2. **Llama 3.3 70B Instruct (Q4_K_M) with CPU offload** — secondary candidate. If 30–90s per preview is tolerable for batch admin generation (it should be — admin already operates async via Hangfire), the quality jump is worth measuring.

Same model serves both MLB and Division II. If size proves wrong for one but not the other, the factory + AppConfig already supports per-sport `Model` overrides — defer until you actually hit the problem.

**Important — production quality reality check.** A 3060 Ti at 8 GB VRAM is not an inference workhorse. Output on a 7B model will be visibly weaker than DeepSeek-chat. That's consistent with the user's framing that MLB and Division II don't need production-quality previews. If at some point one of these tiers is promoted to "users actually care," the answer is paid DeepSeek for that tier, not a Bender hardware upgrade.

**GPU upgrade considered and rejected on cost.** A 24+ GB card (RTX 3090 used, RTX 4090, RTX A5000) starts at ~$700 used and runs $1,250+ new. The expected return — better-quality MLB and Division II previews for audiences that aren't prioritized — doesn't justify the spend. The Bender hardware should be treated as fixed at i9-12900K / 3060 Ti / 128 GB. Model selection lives within that envelope.

### Decision 6 — The OllamaClient global semaphore

`SemaphoreSlim(1, 1)` static lock in both clients. For DeepSeek it's defensible. For Ollama it's pointless serialization.

**Recommendation:** remove from `OllamaClient` whenever the factory lands. If you want a concurrency cap, set HttpClient `MaxConnectionsPerServer`. Don't touch DeepSeek's semaphore.

### Decision 7 — Multi-sport TODOs (blocker)

`MatchupPreviewProcessor.cs` has two `TODO: multi-sport` markers (lines 54 and 72). The processor is currently hardcoded to `Sport.FootballNcaa` (and effectively to FBS — see the FBS-anchored SQL queries it depends on). MLB and Division II previews don't run today regardless of provider — so neither plumbing pilot can start until those close.

The shape: lift `Sport` out of the command, route the contest lookup and franchise-stat fetch through the correct sport's clients, pick the right prompt blob per sport (e.g. `prediction-insights-mlb-v1.txt`). This is its own PR before the factory work.

The Division II case under NCAAFB is more subtle: the existing FBS-anchored SQL won't return Division II contests at all. Either the processor learns to anchor at the Division II root group when the contest is Div II, or the prompt-data pipeline becomes division-agnostic. This is a Phase 1 design question — call it out, don't try to solve in this doc.

### Decision 8 — Fallback when Ollama is down

If Bender's GPU OOMs or the network blips, MLB and Division II preview generation will 500. Two stances:

- **Don't build fallback.** MLB and Div II are explicitly low-cost-tolerated. Admin retries manually.
- **Build a "try Ollama, fall back to DeepSeek" decorator.** Costs DeepSeek tokens when Bender flakes — directly counter to the cost-saving motivation.

**Recommendation:** don't build it. The user's framing is explicit — paid DeepSeek is reserved for FBS and NFL where audience care merits it. Falling back to DeepSeek for the cheap tiers partially defeats the intent. Revisit only if Bender uptime turns out to be operationally painful.

### Decision 9 — Prompt + output schema co-versioning

`GameRecapPromptProvider.cs:42` has a TODO and a fallback that emits `game-recap-v2` while the prompt blob is `game-recap-v1.txt`. The `MatchupPreview` entity stamps `PromptVersion`, but the contract between prompt and parser is implicit:

- The recap handler extracts title by splitting on a `*` delimiter
- The matchup preview parser tries V1 schema then falls back to V2

Different models will emit slightly different formatting. With DeepSeek and Ollama both in production across different sports/divisions, divergent output shapes will appear silently. Tightening this — JSON-only outputs, no surrounding prose, prompt-version + schema-version bound together — pays off the moment both providers are live.

Not a blocker, but high-value to land before Phase 3.

---

## Proposed rollout

### Phase 0 — Discovery (no code changes)

- Inventory Bender (GPU, RAM, OS, network position)
- Confirm reachability path (Decision 4)
- Pull candidate models, run them through saved MLB and Division II fixtures
- Capture JSON parse rate and average latency

### Phase 1 — Multi-sport plumbing (separate PR, blocker for everything else)

- Close the two `TODO: multi-sport` markers in `MatchupPreviewProcessor`
- Decide the FBS-anchored SQL question for Division II (Decision 7)
- Add MLB-flavored prompt blobs (`prediction-insights-mlb-v1.txt`, `game-recap-mlb-v1.txt`)
- Add Division II prompt blobs if the editorial voice differs from FBS — likely it should be lighter/shorter given the audience
- Verify the end-to-end flow runs against DeepSeek for MLB and Division II first — proves the plumbing is provider-independent

### Phase 2 — Sport+division-aware factory + AppConfig

- Introduce `IAiClientFactory` / `AiClientFactory` mirroring `IFranchiseClientFactory`
- Remove the direct `IProvideAiCommunication` binding from `Program.cs:212`
- Migrate `MatchupPreviewProcessor` and `GenerateGameRecapCommandHandler` to use the factory
- Add the `CommonConfig:AiProviderRouting:{Sport}[:{Division}]` keys; explicit entries only for FBS (DeepSeek) and NFL (DeepSeek). NCAAFB `Default`, MLB → Ollama once Phase 3 starts. Missing keys fail closed.
- Remove the ngrok header and global semaphore from `OllamaClient`

### Phase 3 — Ollama enablement for MLB and NCAAFB sub-FBS

- Stand Bender up on the chosen Ollama model
- Set `CommonConfig:AiProviderRouting:BaseballMlb = Ollama` and `CommonConfig:AiProviderRouting:FootballNcaa:Default = Ollama` in `Prod.All`
- Set `CommonConfig:OllamaClientConfig:BaseUrl` to Bender's chosen reachability path
- Run admin-trigger MLB and Division II preview generation; verify rows land with `Model = "<ollama model name>"`
- Watch parse failure rate over a few weeks of MLB + Division II

### Phase 4 — Stabilization

- If parse failure rate is acceptable, the plumbing is proven
- DeepSeek continues to serve FBS + NFL indefinitely
- When NBA / NHL / PGA onboard, add explicit AppConfig entries — Ollama for plumbing-first sports, DeepSeek for production-quality from day one

### Phase 5 — Schema hardening (future, separate)

- Decision 9's prompt+schema co-versioning work
- Becomes higher-value as more provider-sport pairs are live

---

## Open questions

These should resolve before Phase 0 completes:

1. ~~**Bender hardware.**~~ Resolved: i9-12900K / RTX 3060 Ti (8 GB VRAM) / 128 GB RAM. See Decision 5.
2. **Bender's network position.** Same subnet as K3s nodes? Different physical network? Anything between them?
3. **Ollama install state on Bender** — already running, or fresh install? What OS?
4. **FBS-anchored SQL → Division II.** Does the Phase 1 plumbing work generalize the existing FBS queries, or does Division II get a parallel set?
5. **FCS classification.** Treat as "lower-tier → Ollama" by default, or carve out as its own routing key? Probably former, but flag for confirmation.
6. **MLB prompt authorship + Division II prompt authorship.** Who writes these? Editorial work.
7. **Cross-division NCAAFB matchups.** Routing tiebreak (FBS plays FCS, etc.) — see Decision 3, deferred to Phase 1.

---

## Out of scope

- **Replacing DeepSeek for FBS or NFL.** Explicitly out — both stay on DeepSeek long-term.
- **Fine-tuning Ollama on sportDeets data.** Future, not part of plumbing.
- **Embeddings / retrieval-augmented generation.** Same.
- **Streaming responses.** Current flow is Hangfire-backed request/response.
- **Multi-model routing within Ollama.** One Ollama model serves all Ollama-routed sports for now.
- **DeepSeek failover for Ollama sports.** Decision 8 says no; revisit only if Bender uptime is painful.
- **Per-conference routing within FBS.** All FBS goes to DeepSeek; no carve-out for Power 4 vs Group of 5.

---

## Files referenced

Production code:

- `src/SportsData.Core/Infrastructure/Clients/AI/IProvideAiCommunication.cs`
- `src/SportsData.Core/Infrastructure/Clients/AI/DeepSeekClient.cs`
- `src/SportsData.Core/Infrastructure/Clients/AI/OllamaClient.cs`
- `src/SportsData.Api/Infrastructure/Prompts/MatchupPreviewPromptProvider.cs`
- `src/SportsData.Api/Infrastructure/Prompts/GameRecapPromptProvider.cs`
- `src/SportsData.Api/Application/Admin/Commands/MatchupPreviewProcessor.cs`
- `src/SportsData.Api/Application/Admin/Commands/GenerateGameRecapCommandHandler.cs`
- `src/SportsData.Api/Controllers/AdminController.cs`
- `src/SportsData.Api/Controllers/PreviewController.cs`
- `src/SportsData.Api/Infrastructure/Data/Entities/MatchupPreview.cs`
- `src/SportsData.Api/Program.cs` (DI registration, line ~212)
- `src/SportsData.Producer/Application/GroupSeasons/GroupSeasonsService.cs` (existing FBS root-group lookup)
- `src/SportsData.Producer/Application/Competitions/Commands/RefreshCompetitionMedia/RefreshAllCompetitionMediaCommandHandler.cs` (existing FBS conditional filtering pattern)

Existing pattern references for the factory:

- `src/SportsData.Core/Infrastructure/Clients/Franchise/IFranchiseClientFactory.cs`
- `src/SportsData.Core/Infrastructure/Clients/Season/ISeasonClientFactory.cs`

Background docs in this folder:

- `DeepSeek_GameRecap_Summary.md` — happy-path overview (DeepSeek-specific)
- `GameRecapGeneration_Guide.md` — full setup + test guide (DeepSeek-specific)
- `QuickStart_GameRecap.md` — 3-minute onboarding for game-recap testing
- `LLMs.md` — older strategy doc; stale Azure-ACA assumptions; historical only
