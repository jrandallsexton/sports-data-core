# Model families: one lab seat across model versions

**Status:** built 2026-10-15 (branch feat/model-family, stacked on #841); owner accepted all four recommendations below
**Parent design:** [model-consensus-lab.md](model-consensus-lab.md)
**Trigger:** PR #841. OpenRouter retired `qwen/qwen3-max` on 2026-10-09,
so it was replaced with Qwen 3.8 Max (0902) as a new `Model` row and the old
row was deactivated. Vortex then pointed out that the matrix renders only active
models. Every past week lost its Qwen column, and that week's consensus fell
from 7 voters to 6, which brings back the 3-3 ties the seventh seat was added
to prevent.

## Goal

Group every version of a model under one **family** (Qwen 3 Max, Qwen 3.8 Max,
and later versions are all "Qwen"). The lab matrix then shows a family's history
together, whichever version produced each pick. Results stay attributed to the
exact version that produced them. Retiring a version should never rewrite
history, and adding a version should never change the panel size.

Not in scope: changing the consensus rule, a server-side scoring harness, and
cross-family analytics.

## Current state (verified 2026-10-14)

- `Model` (`Infrastructure/Data/Entities/Model.cs`) has no family or version
  concept. Name is unique, (ModelProviderId, ApiModelId) is unique, and a
  partial index `IX_Model_SingleDefault` allows only one IsDefault row.
- **Panel fan-out** (`PreviewsController.cs:245-257`): `Models.Where(IsActive
  && ModelProvider.IsActive)`, then `AiModelClientResolver.CanResolve` (today
  that means `Gateway == OpenRouter`). One experiment runs per surviving model.
- **Matrix** (`GetModelLabMatrixQueryHandler.cs`): the columns use the same
  filter (`:160-176`), and captures are dropped unless their ModelId is a
  current column (`:115`). The latest capture per (contest, model) wins
  (`:116-118`). **This is the defect:** a retired model's captures still exist
  but are never shown.
- **Captures** (`MatchupPreviewPrompt`) store `Model` (the ApiModelId
  string, which is the record of provenance) and `ModelId` (Guid?, no FK).
  `MatchupPreview.ModelId` is stamped only from the IsDefault row.
- **Consensus and records are computed in the browser**
  (`AdminModelLabPage.jsx:412-442`; `consensusOf` `:713-721` takes a majority
  of the votes cast, needs at least 2 votes, and returns null on a tie). Records
  are keyed by model id.
- **Contamination risk is computed only in the browser**
  (`AdminModelsPage.jsx:17-30` `riskFor2025`, per model). No server-side harness
  exists yet.
- The Model Manager (`AdminModelsPage.jsx`) creates a model with a provider and
  edits metadata plus IsActive. Name, ApiModelId and provider cannot change
  after creation (`UpdateModelCommand`).

## Design

### Data

New table **`ModelFamily`** (singular, `CanonicalEntityBase<Guid>`):

| Column | Notes |
|---|---|
| `Id` | fixed UUIDs for the seeded families |
| `Name` | unique, max 100. Examples: "Qwen", "Claude Sonnet" |
| `Description` | nullable, max 512 |

**Why not use the provider?** A provider is too coarse: Anthropic already
holds two seats (Sonnet and Haiku). A family is the lineage that a lab seat
follows.

**`Model.ModelFamilyId`**: a required FK with `DeleteBehavior.Restrict`.

**One seat per family:** a partial unique index enforced by the database,

```
IX_Model_SingleActiveLabSeat  ON "Model" ("ModelFamilyId")
  WHERE "IsActive" AND "Gateway" = 1
```

This makes "the seventh seat" a constraint instead of a convention. The
fan-out runs at most one version per family, so a version swap can never turn
7 voters into 8 or 6.

`Gateway = 1` is included because the direct DeepSeek production row
(Gateway None, IsDefault, not a matrix column) belongs to the DeepSeek family
next to the OpenRouter DeepSeek V3.1 audition row. Both are legitimately
active. The filter mirrors today's `CanResolve`. When the promotion rule
brings first-party clients into the panel, `CanResolve` and this filter change
together (see open questions).

### Seeded families and backfill

| Family | Model rows |
|---|---|
| Claude Sonnet | b…01 Claude Sonnet 4.6 |
| Claude Haiku | b…02 Claude Haiku 4.5 |
| GPT | b…03 GPT-5.2 |
| Gemini | b…04 Gemini 3.1 Pro |
| Grok | b…05 Grok 4.3 |
| DeepSeek | b…06 DeepSeek V3.1 (OpenRouter), b…07 DeepSeek Chat (direct, production) |
| Qwen | b…08 Qwen 3 Max (retired), b…09 Qwen 3.8 Max (0902) |

**The migration must not crash the API.** Migrations auto-apply at API
startup (`Program.cs:486-489`), so any statement that fails puts the API in a
crash loop. The migration therefore:

1. Creates `ModelFamily` and inserts the seven families (fixed UUIDs).
2. Adds `ModelFamilyId` as **nullable** and maps the known rows by their fixed
   Model Ids.
3. Gives each remaining row with no family (models an operator created in the
   Model Manager) a family by name: it joins an existing family whose name
   equals the model's name (a model named "GPT" joins the seeded GPT family),
   otherwise it gets **its own family, named after the model**. An operator
   can regroup it later.
4. **Resolves seat collisions before creating the index.** In each family with
   more than one active OpenRouter row, it keeps one active and deactivates
   the rest: the production default (`IsDefault`) first, otherwise the newest
   by `ReleaseDate` (ties broken by `CreatedUtc`), with a
   `RAISE NOTICE` for each change. The case it handles is any database that
   never ran `model_lab_2026-10-14_qwen38_max.sql`, where Qwen 3 Max and 3.8
   are both active. The model it deactivates returns 404 anyway.
5. Sets the column NOT NULL, then adds the FK and the partial unique index.

On a fresh database the `Model` table is empty when the migration runs, so
steps 2-4 change nothing. `seed_model_lab.sql` then has to insert families
before models, and give every model row its `ModelFamilyId`.

### Matrix: columns are families

`ModelLabMatrixDto`:

- `Models` is renamed **`Families`**: `{ Id, Name, ActiveModelId?,
  ActiveModelName? }`. The active model is the family's one lab-reachable
  active model, or null when no version of the family is active any more.
  Renamed because the columns now are families.
- `MatrixCellDto` gains **`FamilyId`** and **`ModelName`**. `ModelId` stays as
  the exact provenance.

Handler:

1. **Active families** are those with an active, lab-reachable model (the
   existing filter, grouped by family).
2. The captures query is unchanged. Each capture's `ModelId` is joined to
   `Model.ModelFamilyId`; captures whose ModelId matches no row (none are
   expected, because there is no delete endpoint) are dropped and logged.
3. **Columns** are the active families plus any family that has a capture in
   this week's contest set, ordered by family Name. A fully retired family
   still shows in the weeks it voted.
4. **Cells:** the latest capture per (contest, **family**) wins, which extends
   the current "latest run wins" rule from model to family.

### Web UI (`AdminModelLabPage.jsx`)

- **Column header:** the family name, with the active version as a subtitle.
  A family with no active version renders read-only: no generate or retry
  button, and it is excluded from the "run panel" hole check.
- **Cell:** when `cell.modelId != column.activeModelId` (a pick by an older
  version), it shows a small version badge (`ModelName`). The drill-down is
  unchanged because it is keyed by `captureId`.
- **Records and consensus** are keyed by family id. Consensus semantics are
  unchanged (one vote per family per contest).
- **Queued markers** use the key `${contestId}|${familyId}`. The generate
  button sends the column's `activeModelId`, the same as today.
- **Print mode:** the letters remain index-based over the columns.
- Fix in passing: the orphaned `consensusOf` doc comment at `:661`.

### Model Manager (`AdminModelsPage.jsx` and commands)

- `GET/POST api/model-families` (admin), plus `ModelFamilyDto { Id, Name,
  Description, ModelCount, ActiveModelName? }`.
- `CreateModelCommand` requires `ModelFamilyId`. The create form gets a family
  picker with an inline "new family" option.
- `ModelDto` gains `ModelFamilyId` and `ModelFamilyName`, and the list groups
  rows by family.
- **Activating a version swaps the seat.** When creating or updating an
  OpenRouter model with IsActive true while its family already has an active
  OpenRouter model, the previous one is deactivated in the same transaction
  and logged. The API response stays a bare Guid. The Model Manager names the
  replaced version in its toast, computed from the list it already holds. This follows the IsDefault
  precedent (`CreateModelCommand :124-135` clears the previous default). The
  index is the backstop if two such requests race.
- `ModelFamilyId` can change after creation (`UpdateModelCommand`), unlike
  provider. Moving an active OpenRouter version into a family takes that
  family's seat.

### Contamination risk stays per version

A family record can mix versions with different cutoffs: Qwen 3 Max had a
clean Jun 30 2025 cutoff, while 3.8 Max (Sep 2026, cutoff unpublished) is
higher-risk on 2025 games. Risk belongs to the capture's model, not to the
column. In this change the cell's version badge makes the mix visible. When
the scoring harness is built, it must classify each capture by
`capture.ModelId` → `KnowledgeCutoffUtc`, never by the family's current
version.

## Rollout

1. One PR: migration, entity, family endpoints, command changes, matrix
   handler and DTO, both admin pages, seed SQL, and tests. The matrix DTO
   rename breaks the web client until both deploy. Both are admin-only, so
   deploying the API then the web client back to back is acceptable (no
   compatibility shim).
2. **Order matters.** Run #841's `model_lab_2026-10-14_qwen38_max.sql` BEFORE
   this deploy. Its INSERT has no `ModelFamilyId`, so it fails NOT NULL once
   this migration has run. If it was missed, create Qwen 3.8 Max in the Model
   Manager (Qwen family) instead; the seat swap retires 3 Max.
3. API deploy: the migration auto-applies. Seat-collision NOTICEs go to the
   Postgres log, not necessarily the API log. Verify afterwards with
   `SELECT "ModelFamilyId", count(*) FROM "Model" WHERE "IsActive" AND
   "Gateway" = 1 GROUP BY 1 HAVING count(*) > 1` (expect no rows; the index
   guarantees it).
4. Verify: a 2025 week that Qwen 3 Max voted in shows a Qwen column with
   version-badged cells. A current week shows Qwen with 3.8. Panel fan-out
   still enqueues 7.
5. Update `model-consensus-lab.md`: link this doc, describe seats as families,
   and fix the stale ":83 Rows = models" (columns are models).

## Testing

- Done 2026-10-15: the migration's generated SQL was dry-run against local
  `sdApi.All` (restored prod copy) inside transactions that rolled back:
  as-is (8 rows map to 7 families, both indexes created); Qwen 3.8 inserted
  beside 3 Max (one NOTICE, 3 Max deactivated); an operator-created model
  (its own auto-created family); fresh database plus the new seed (9 rows,
  Qwen 3 Max inactive).
- Migration: apply locally to a restored copy with Qwen 3 Max and 3.8 both
  active (with the swap script not run). Expect one NOTICE, 3 Max
  deactivated, the index created, and the API booting.
- `GetModelLabMatrixQueryHandlerTests` (only 2 facts today) gain:
  - a retired version's captures land in its family column;
  - a fully retired family appears only in weeks it voted;
  - the latest capture per (contest, family) wins across versions;
  - a family with no active version has a null `ActiveModelId`.
- Command tests:
  - create requires a family;
  - activating a version swaps the seat;
  - the swap is limited to OpenRouter rows (the direct DeepSeek row is
    unaffected).
- Fan-out: there are no tests for the `PreviewsController` panel today. The
  index guarantees one row per family, so the fan-out code does not change.

## Decisions (owner, 2026-10-15: all four recommendations accepted)

1. **Can family be changed after creation?** Recommend yes: grouping is an
   analysis decision, not identity, and regrouping is exactly how an
   auto-created family (migration step 3) gets fixed. Provider stays
   immutable.
2. **Family-level active switch?** Recommend no. A family is "on" when one of
   its versions is active, and a single switch avoids two flags that can
   disagree.
3. **Seat filter after promotion:** when a direct first-party client joins the
   panel, `CanResolve` returns true for Gateway None. The index filter should
   then become "lab-reachable", which is likely a persisted `IsLabSeat` flag,
   not a gateway test. That is deferred to the promotion work and noted here
   so it isn't forgotten.
4. **Rerunning a past week with a new version** replaces the old version's
   vote in that cell, because latest wins per family. That matches today's
   rerun semantics, but for 2025 backtests it means swapping a lower-risk vote
   for a higher-risk one. **Accepted:** latest wins per family, as with
   today's reruns. The version badge shows which version produced the cell,
   and the replaced capture stays in the Preview Lab history.
