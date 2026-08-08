# LLM Model Registry — knowledge cutoffs vs the 2025 season

Seed data for the coming `ModelProvider`/`Model` tables (design:
`matchup-preview-data-inputs.md` §6). Compiled 2026-08-08 from provider
documentation and cross-checked aggregators; every row needs
re-verification against the provider's own docs when entered into the
Model table (that is what the EvidenceSource/VerifiedUtc columns are
for). Risk labels are relative to the 2025 season: NCAA week 0/1 =
Aug 23 – Sep 1, 2025; NFL kickoff Sep 4, 2025; Super Bowl Feb 8, 2026.

**Cutoff semantics matter:** contamination cares about the TRAINING
data cutoff, not the "reliable knowledge" date some providers also
publish. Where the two differ, the LATER (training) date governs the
risk label. Any model run with server-side retrieval/search enabled
(e.g. Grok's Web/X search) is contaminated regardless of cutoff —
backtests must disable retrieval tools.

## Anthropic (user-supplied from provider docs, 2026-08-08)

| Model | API id | Released | Training cutoff | 2025-season risk |
|---|---|---|---|---|
| Claude Opus 5 | claude-opus-5 | 2026 | May 2026 | Higher — full season |
| Claude Sonnet 5 | claude-sonnet-5 | 2026 | Jan 2026 | Higher — Sep–Dec |
| Claude Fable 5 | claude-fable-5 | Jun 2026 | Jan 2026 | Higher — Sep–Dec |
| Claude Opus 4.8 | claude-opus-4-8 | May 2026 | Jan 2026 | Higher — Sep–Dec |
| Claude Opus 4.7 | claude-opus-4-7 | Apr 2026 | Jan 2026 | Higher — Sep–Dec |
| Claude Sonnet 4.6 | claude-sonnet-4-6 | Jan 2026 | Aug 2025 ⚠️ | Lower (NFL + NCAA wk2+) — **VERIFY** |
| Claude Opus 4.6 | claude-opus-4-6 | 2025 | Aug 2025 | Lower (NFL + NCAA wk2+) |
| Claude Haiku 4.5 | claude-haiku-4-5 | Oct 2025 | Jul 2025 | Lower — full season (cheap floor) |
| Claude Opus 3 | (family label — resolve exact id) | 2024 | Aug 2023 | Lower but 2 generations old — skip |

⚠️ **Sonnet 4.6 discrepancy (load-bearing — resolve before trusting):**
one cross-check source claims training data through Jan 2026 with only
"reliable knowledge" at Aug 2025. If TRAINING ran to Jan 2026, Sonnet
4.6 flips to higher-risk and Opus 4.6 deserves the same scrutiny.
Verify against docs.anthropic.com before using either as a
lower-risk backtest model.

## OpenAI

| Model | API id | Released | Training cutoff | 2025-season risk |
|---|---|---|---|---|
| GPT-5.5 / 5.5 Pro | gpt-5.5 / gpt-5.5-pro | Apr 2026 | Dec 1, 2025 | Higher — Sep–Nov |
| GPT-5.4 / 5.4 Pro | gpt-5.4 / gpt-5.4-pro | Mar 2026 | Aug 31, 2025 | Lower for NFL + NCAA wk2+; NCAA wk0/1 INSIDE cutoff |
| GPT-5.2 | gpt-5.2 | Dec 2025 | Aug 31, 2025 | Same as 5.4 |
| GPT-4o | gpt-4o | May 2024 | Oct 2023 | Lower — full season (older capability) |

Note the Aug 31, 2025 cutoffs: NCAA week 0/1 (Aug 23–31) sits INSIDE
training for 5.4/5.2 — exactly why per-game
`StartDateUtc >= Model.KnowledgeCutoffUtc` comparison beats per-season
labels; the scoring query handles this boundary automatically.

## Google

| Model | API id | Released | Training cutoff | 2025-season risk |
|---|---|---|---|---|
| Gemini 3.5 Pro / Flash | gemini-3.5-pro / -flash | May 2026 | Jan 2025 ⚠️ conflicting claims (one source says Jan 2026) | Lower if Jan 2025 — **VERIFY via model card** |
| Gemini 3.1 Pro | gemini-3.1-pro-preview | Feb 2026 | Jan 2025 | Lower — full season, current capability |
| Gemini 2.5 Pro | gemini-2.5-pro | Mar 2025 | Jan 2025 | Lower — full season |

If Jan 2025 holds for the 3.x line, Google offers the rare combo:
CURRENT capability + lower-risk for the whole 2025 season.

## DeepSeek (incumbent — provider does NOT publish cutoffs)

| Model | API id | Released | Training cutoff | 2025-season risk |
|---|---|---|---|---|
| DeepSeek V4-Pro / V4-Flash | (per API docs) | Apr 2026 | UNPUBLISHED | Treat as higher — unknown = worst case |
| DeepSeek V3 / R1 | deepseek-chat / deepseek-reasoner (verify) | Dec 2024 / Jan 2025 | ~Jul 2024 (inferred, unofficial) | Lower — full season, but undocumented |

Action item: confirm which DeepSeek model id the current
IProvideAiCommunication binding actually calls — that determines the
incumbent's own risk label.

## xAI

| Model | API id | Released | Training cutoff | 2025-season risk |
|---|---|---|---|---|
| Grok 4.5 | (per docs.x.ai) | 2026 | Feb 1, 2026 | Higher — full season |
| Grok 4.3 | (per docs.x.ai) | 2025/26 | Dec 2025 | Higher — Sep–Nov |

Grok's server-side Web/X search MUST be disabled for any backtest run.

## Others (cross-checked aggregator, Tier-2 confidence)

| Model | Cutoff | 2025-season risk |
|---|---|---|
| Qwen 3 Max | Jun 30, 2025 | Lower — full season |
| Kimi K2.6 | Apr 2025 | Lower — full season, current (Apr 2026) release |
| Kimi K2 0905 | Dec 2024 | Lower — full season |
| MiniMax M2.5 | Jan 2025 | Lower — full season |
| MiniMax M3 | Jan 2026 | Higher — Sep–Dec |

## The lower-risk backtest pool (pending the flagged verifications)

Full-season lower-risk candidates with modern capability: Gemini
3.1/3.5 line (if Jan 2025 verifies), Claude Haiku 4.5, Qwen 3 Max,
Kimi K2.6, MiniMax M2.5, DeepSeek V3/R1 (undocumented caveat), GPT-4o
(older). Near-full-season (NFL + NCAA wk2+): Claude Sonnet/Opus 4.6
(pending the training-cutoff verification), GPT-5.4/5.2.

Sources: user-supplied Anthropic list; metehan.ai LLM cutoff registry
(2026-07-04); ai.google.dev changelog; docs.x.ai; DeepSeek V4 coverage
(morphllm/codersera); HaoooWang/llm-knowledge-cutoff-dates.
