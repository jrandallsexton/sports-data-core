-- Model Consensus Lab seed: providers + an initial OpenRouter-routed
-- audition roster. Source of truth for cutoffs/risk:
-- docs/metrics-modeling/llm-training-dates.md; design:
-- docs/features/model-consensus-lab.md.
--
-- ApiModelIds are OpenRouter's namespaced ids, verified against the live
-- catalog 2026-09-03 (exact ids only — aliases silently swap weights).
-- Costs are OpenRouter pass-through per-MTok as of the same date.
-- CutoffVerifiedUtc is deliberately NULL everywhere: cutoffs here are
-- transcribed claims — verify against provider docs in the Model Manager
-- (that is what the evidence/verified columns are for).
--
-- Idempotent: fixed UUIDs + ON CONFLICT DO NOTHING (any unique clash —
-- id, name, or (provider, api id) — skips the row rather than erroring).
-- Target DB: API (AppDataContext).

-- Providers. Kind maps to a first-party client implementation;
-- 99 = GatewayOnly (no first-party client; reachable only via gateway).
INSERT INTO "ModelProvider" ("Id", "Name", "Kind", "Description", "IsActive", "CreatedUtc", "CreatedBy")
VALUES
  ('a0000000-0000-0000-0000-000000000001', 'DeepSeek',  0, 'Incumbent production provider (direct client exists)', TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
  ('a0000000-0000-0000-0000-000000000002', 'Anthropic', 1, NULL, TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
  ('a0000000-0000-0000-0000-000000000003', 'OpenAI',    2, NULL, TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
  ('a0000000-0000-0000-0000-000000000004', 'Google',    3, NULL, TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
  ('a0000000-0000-0000-0000-000000000005', 'xAI',       99, 'Gateway-only (no first-party client); disable retrieval for backtests', TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),
  ('a0000000-0000-0000-0000-000000000006', 'Alibaba',   99, 'Gateway-only (no first-party client)', TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000')
ON CONFLICT DO NOTHING;

-- Models, all Gateway = 1 (OpenRouter). KnowledgeCutoffUtc = declared
-- TRAINING cutoff (NULL = unpublished, harness treats as higher-risk).
INSERT INTO "Model" (
  "Id", "ModelProviderId", "Name", "ApiModelId", "Gateway",
  "ReleaseDate", "KnowledgeCutoffUtc", "CutoffEvidence", "CutoffVerifiedUtc",
  "InputCostPerMTok", "OutputCostPerMTok", "IsActive", "IsDefault",
  "CreatedUtc", "CreatedBy")
VALUES
  ('b0000000-0000-0000-0000-000000000001', 'a0000000-0000-0000-0000-000000000002',
   'Claude Sonnet 4.6', 'anthropic/claude-sonnet-4.6', 1,
   '2026-02-17', '2025-08-01', 'llm-training-dates.md: Aug 2025 — DISPUTED (one source claims training to Jan 2026); verify at docs.anthropic.com before trusting lower-risk', NULL,
   3.00, 15.00, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  ('b0000000-0000-0000-0000-000000000002', 'a0000000-0000-0000-0000-000000000002',
   'Claude Haiku 4.5', 'anthropic/claude-haiku-4.5', 1,
   '2025-10-15', '2025-07-01', 'llm-training-dates.md: Jul 2025 (lower-risk full 2025 season; cheap floor)', NULL,
   1.00, 5.00, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  ('b0000000-0000-0000-0000-000000000003', 'a0000000-0000-0000-0000-000000000003',
   'GPT-5.2', 'openai/gpt-5.2', 1,
   '2025-12-10', '2025-08-31', 'llm-training-dates.md: Aug 31 2025 — NCAA wk0/1 sits INSIDE training; per-game comparison handles the boundary', NULL,
   1.75, 14.00, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  ('b0000000-0000-0000-0000-000000000004', 'a0000000-0000-0000-0000-000000000004',
   'Gemini 3.1 Pro', 'google/gemini-3.1-pro-preview', 1,
   '2026-02-19', '2025-01-01', 'llm-training-dates.md: Jan 2025 (lower-risk full season + current capability); verify via model card', NULL,
   2.00, 12.00, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  ('b0000000-0000-0000-0000-000000000005', 'a0000000-0000-0000-0000-000000000005',
   'Grok 4.3', 'x-ai/grok-4.3', 1,
   '2026-04-30', '2025-12-01', 'llm-training-dates.md: Dec 2025 (higher-risk Sep-Nov); server-side Web/X search MUST stay disabled for backtests', NULL,
   1.25, 2.50, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  ('b0000000-0000-0000-0000-000000000006', 'a0000000-0000-0000-0000-000000000001',
   'DeepSeek V3.1', 'deepseek/deepseek-chat-v3.1', 1,
   '2025-08-01', NULL, 'Provider publishes no cutoff (~Jul 2024 inferred, unofficial) — NULL = treated higher-risk by design', NULL,
   0.25, 0.95, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  -- The SEVENTH SEAT (added 2026-09-03): an odd audition panel makes the
  -- matrix's strict-majority consensus decidable whenever every model
  -- votes — six voters pushed 3-3 too often. Qwen 3 Max is the top of
  -- llm-training-dates.md's lower-risk pool (Jun 30 2025 cutoff, full
  -- 2025 season clean) AND a fourth architecture family — a tiebreaker
  -- correlated with existing panelists would just vote with the herd.
  ('b0000000-0000-0000-0000-000000000008', 'a0000000-0000-0000-0000-000000000006',
   'Qwen 3 Max', 'qwen/qwen3-max', 1,
   '2025-09-01', '2025-06-30', 'llm-training-dates.md: Jun 30 2025 (Tier-2 aggregator confidence — verify against Alibaba docs)', NULL,
   0.78, 3.90, TRUE, FALSE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000'),

  -- The production incumbent, reached DIRECTLY (Gateway 0 = None) — the
  -- same weights as the OpenRouter row above but a different evaluand.
  -- IsDefault TRUE: production Generate stamps MatchupPreview.ModelId
  -- from this row when its ApiModelId matches the wired DeepSeek client
  -- (CommonConfig:DeepSeekClientConfig:Model = "deepseek-chat").
  -- Gateway None is unreachable by the lab resolver, so this row is NOT
  -- a matrix column — it exists as production provenance.
  ('b0000000-0000-0000-0000-000000000007', 'a0000000-0000-0000-0000-000000000001',
   'DeepSeek Chat (production, direct)', 'deepseek-chat', 0,
   NULL, NULL, 'Provider publishes no cutoff — NULL = treated higher-risk by design', NULL,
   0.27, 1.10, TRUE, TRUE, NOW() AT TIME ZONE 'utc', '00000000-0000-0000-0000-000000000000')
ON CONFLICT DO NOTHING;
