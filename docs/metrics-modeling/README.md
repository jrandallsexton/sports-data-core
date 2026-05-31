# docs/metrics-modeling

This folder collects the AI / LLM / prediction-modeling design and reference docs for sportDeets.

The grouping is broader than the name implies — it covers both:

- **AI (LLM) workstreams** — prompt-driven game recaps and matchup previews, the existing DeepSeek integration, the planned Ollama-on-Bender cutover
- **Numerical prediction workstreams** — the Python `src/metrics-modeling/` prototype, the deetsMeter feature, the planned cluster-hosted prediction microservice

The two halves are deliberately kept in the same folder so design crosstalk (model identity, MetricBot user pattern, observability) stays adjacent.

## Active design

- [ai-provider-routing-per-sport.md](./ai-provider-routing-per-sport.md) — design for per-`(sport, division)` AI provider routing. DeepSeek stays for NCAAFB FBS and NFL (paid, audience matters). Ollama on Bender lights up for MLB and NCAAFB Division II (and any lower-tier divisions) where editorial accuracy isn't a release criterion. `OllamaClient` already exists; the work is wiring a sport-and-division-aware factory.
- [metrics-microservice-deetsmeter.md](./metrics-microservice-deetsmeter.md) — design for promoting the prototype Python scripts into a cluster-hosted FastAPI service. Greenfield work; multiple open decisions before implementation begins.

## Background reference (DeepSeek era)

These predate the Ollama cutover plan and may contain stale claims (e.g. `LLMs.md` references Azure Container Apps, which the platform has moved away from). Treat as historical, not as source of truth.

- [DeepSeek_GameRecap_Summary.md](./DeepSeek_GameRecap_Summary.md) — high-level overview of the DeepSeek-based game-recap solution
- [GameRecapGeneration_Guide.md](./GameRecapGeneration_Guide.md) — full setup + test guide for game recap generation against DeepSeek
- [QuickStart_GameRecap.md](./QuickStart_GameRecap.md) — 3-minute onboarding for game recap testing
- [LLMs.md](./LLMs.md) — earlier strategy doc weighing self-hosted vs managed LLM options
