# Team mark batch generation — execution plan

Companion to `marks.js` (the engine) and `app.js` (the demo harness).
This doc covers how we turn the engine into batch-generated team
marks stored in Azure Blob and indexed in `FranchiseLogo` /
`FranchiseSeasonLogo`. Background and the why-not-real-logos context
live in `docs/team-mark-design-brief.md`.

## Context

We cannot ship real team logos to the app stores (licensing is
cost-prohibitive and unlicensed marks will be flagged in review).
Claude Design produced `marks.js` — a deterministic, pure-string
SVG generator that takes (primary color, secondary color,
abbreviation, sport) and emits a complete SVG. Selected direction:
**Roundel** (monogram-on-primary, accent ring, theme-agnostic body).

The end state we want:

- Every active `Franchise` and `FranchiseSeason` row has a
  generated-mark PNG stored in Azure Blob.
- That PNG is registered in `FranchiseLogo` / `FranchiseSeasonLogo`
  as a new row, tagged `Rel: ["sportdeets-mark", "default"]`.
- A one-line change in the C# logo-selection code prefers
  `sportdeets-mark` over ESPN-sourced rows.
- Existing ESPN-sourced logo rows stay in place as a revert path
  until we confirm the new marks render correctly app-wide.

## What we have to work with

### `marks.js` (in this folder)

Renderers are pure string concatenation — no DOM dependency in the
renderer functions themselves. Only the IIFE's `window` global
attachment is browser-coupled. Trivially shimmed for Node.

Public API:
- `SDMarks.render('roundel' | 'shield' | 'hex' | ...,team, opt)` returns an SVG string
- `SDMarks.directions` — registry of available directions
- `SDMarks.resolvePalette(primary, secondary)` — color math, used internally

For batch generation we only call `render('roundel', team, { size: 512, theme: 'light' })`.

### Schema (verified against entity classes 2026-06-05)

`Franchise` (`SportsData.Producer/Infrastructure/Data/Entities/Franchise.cs`):
- `Id` (Guid), `Sport` (enum), `Abbreviation` (nullable, 10 chars)
- `DisplayName`, `DisplayNameShort`
- `ColorCodeHex` (required, 7 chars — e.g. `#FFFFFF`)
- `ColorCodeAltHex` (nullable, 7 chars)
- `IsActive` (bool)
- `Logos` — `ICollection<FranchiseLogo>`

`FranchiseSeason` (same folder):
- `Id` (Guid), `FranchiseId` (Guid), `SeasonYear` (int)
- `Abbreviation` (required, 20 chars)
- `DisplayName`, `DisplayNameShort`
- `ColorCodeHex` (required, 7 chars), `ColorCodeAltHex` (nullable, 7 chars)
- `IsActive` (bool)
- `Logos` — `ICollection<FranchiseSeasonLogo>`

`FranchiseLogo` / `FranchiseSeasonLogo` (both implement `ILogo`):
- `Id` (Guid), `FranchiseId` / `FranchiseSeasonId` (Guid)
- `Uri` (required, 256 chars)
- `OriginalUrlHash` (required, 64 chars, indexed — SHA-256 of source URL historically)
- `Width`, `Height` (long, nullable)
- `Rel` — `List<string>` (e.g. `["default"]`, `["scoreboard"]`)
- `IsForDarkBg` (bool, nullable)

### Blob pipeline (`SportsData.Core/Infrastructure/Blobs/BlobStorageProvider.cs`)

- `UploadImageAsync(stream, containerName, filename)` returns Uri,
  retries (exponential, 5 attempts), creates container if missing
  with `PublicAccessType.BlobContainer`.
- ContentType is **hardcoded to `image/png`** in the provider. PNG
  output is the path of least resistance.
- Connection string read from `CommonConfig.AzureBlobStorageConnectionString`.
- Container naming convention from `DocumentController.cs:282-284`:
  `{doc-type-kebab}-{sport-kebab}` or `{doc-type-kebab}-{sport-kebab}-{seasonYear}`.

## Approach: standalone Node batch script

Why Node and not a C# port:

1. **`marks.js` stays the source of truth.** Porting to C# creates
   a maintenance fork — every Claude Design iteration would require
   parallel C# rewrites.
2. **No code change in Producer / Provider / API.** Generated marks
   slot into existing `FranchiseLogo` / `FranchiseSeasonLogo` rows
   via the existing data model. Only logo *selection* (which Rel
   wins) needs to change, and that's a one-liner.
3. **One-time per team / season.** New franchises are rare;
   new seasons are annual. Not a real-time concern that needs
   in-process integration.
4. **Easy to rerun** when Claude Design ships a refined Roundel —
   no service restart, no migration.

Why not run from C#:

- Means embedding a JS engine (Jint, ClearScript) just for SVG
  string output. Possible but adds runtime dependencies for a
  problem Node solves natively.

## Decisions

Decisions made with rationale. Each lists what was considered and
why we picked what we picked.

### Output format → PNG

Considered SVG (smallest, scales perfectly) and PNG.

Picked **PNG** because:
- True drop-in for existing ESPN PNG URLs throughout the stack.
- Existing `BlobStorageProvider` hardcodes `image/png` content type.
- Mobile (React Native) and web (React) both render PNG out of the
  box. SVG would require `react-native-svg` integration plus a
  separate web image component swap — out of scope here.
- SVG can be added later as an enhancement if PNG fidelity proves
  insufficient at large sizes.

### Output size → single 512×512

Considered single (512), multi-size (24/64/256/512).

Picked **single 512×512** because:
- Matches roughly what ESPN logo storage uses today.
- Downscales fine for mobile cards (48-72px) and standings rows
  (24-32px); the roundel design is built to read at small sizes.
- Multi-size is a perf optimization that doesn't matter at our
  current scale. CDN handles repeated downloads.
- Simpler pipeline: one render call, one upload per team.

### Row strategy → insert new rows per direction, keep ESPN rows intact

Considered: insert new rows, replace existing rows, in-place URL swap.

Picked **insert three new `sportdeets-mark`-tagged rows per franchise**
(one per direction — roundel, shield, hex) because:
- Generation is cheap and blob storage is pennies; producing all
  three universal-shape directions buys flexibility to switch the
  active direction later without regeneration.
- Existing ESPN rows become an instant revert path if the new marks
  look wrong in production.
- Logo selection flip (Phase 6) can be A/B'd safely.
- Cleanup of ESPN rows happens as a follow-up after UI confirmed.
- In-place URL swap was rejected — any cached client (Cloudflare,
  RN image cache, browsers) would see stale assets until cache
  expiry.

### Direction tagging → `Rel: ["sportdeets-mark", "{direction}"]`

Each generated row carries the direction id (`roundel`, `shield`,
`hex`) in its `Rel` array. Phase 6's C# logo selection picks the
row whose Rel matches the currently-preferred direction.

How the preferred direction is chosen is intentionally deferred —
options ordered from simplest to most flexible:
1. Hard-coded constant in C# (one-line change to swap, requires deploy)
2. Azure AppConfig value `SportsDeets:PreferredMarkDirection` (config
   change, no deploy)
3. Per-user preference (UI setting, requires schema + API surface)

Start with option 1 in Phase 6 — option 2 is a fast follow-up if we
end up swapping directions during the friend-tester phase.

### Light / dark variants → skip for now

The roundel design is theme-agnostic except for a hairline keyline
that adjusts based on `theme: 'light' | 'dark'`. The body colors
(base, accent, ink) don't change.

Single PNG generated with `theme: 'light'`. Add a dark variant only
if real contrast issues surface in app UI testing.

### Container layout → single container, path-prefixed

Considered: per-sport containers (mirroring existing
`{doc-type}-{sport}-{year}` convention), single flat container.

Picked **single container `sportdeets-marks`** with paths:
- `franchise/{direction}/{FranchiseId}.png`
- `franchise-season/{direction}/{FranchiseSeasonId}.png`

Where `{direction}` is one of `roundel`, `shield`, `hex`.

The per-sport split made sense for ESPN-sourced documents (cache
invalidation per document type per season). Our marks are flat and
uniform — no benefit from per-sport split, simpler operationally.
The direction segment in the path makes it trivial to browse all
roundels (or shields, or hexes) in Azure Storage Explorer.

### Synthetic `OriginalUrlHash`

`FranchiseLogo.OriginalUrlHash` is required and indexed. Historically
it's the SHA-256 of the ESPN source URL. For generated marks there is
no source URL.

Use a deterministic synthetic: `SHA256("sportdeets-mark:{direction}:{Id}")`
where `{direction}` is `roundel`, `shield`, or `hex`.

Unique per (franchise/franchise-season × direction), deterministic so
reruns overwrite cleanly, and obviously synthetic so anyone debugging
recognizes it isn't an ESPN hash. The direction segment in the hash
input ensures the three rows-per-franchise don't collide on the
indexed `OriginalUrlHash` column.

### Database run order → local prod-copy first

Don't point the first run at prod. Run against a local copy of the
prod database, inspect generated marks in a local blob container or
mock, verify the UI renders them correctly, then point at prod.

## Phases

Time estimates are rough. Phases 1-5 are the batch script itself;
Phase 6 is the C# selection flip, which is a separate PR.

| # | Phase | Time | Deliverable |
| - | ----- | ---- | ----------- |
| 1 | UMD-shim `marks.js` so it loads under Node | ~15 min | `node -e "require('./marks')"` works |
| 2 | Add `@resvg/resvg-js` for SVG → PNG | ~30 min | A 512px PNG of one sample team on disk |
| 3 | Postgres data fetch via `pg` | ~30 min | Console-logged list of franchises with color data |
| 4 | Generate + upload per franchise / season | ~1 hr | Blobs visible in `sportdeets-marks` container |
| 5 | Insert `FranchiseLogo` / `FranchiseSeasonLogo` rows | ~30 min | New rows tagged `sportdeets-mark` in DB |
| 6 | C# logo selection prefers `sportdeets-mark` | ~1 hr (separate PR) | Mobile + web UI shows generated marks |

Total: ~3.5 hours of work paced. Phases 1-5 can run as a single PR;
Phase 6 is its own PR with its own UI testing.

### Phase 1 — UMD-shim `marks.js`

Current IIFE: `(function (global) { ... })(window);`

Change to detect Node:

```js
(function (root, factory) {
  var api = factory();
  if (typeof module === 'object' && module.exports) module.exports = api;
  else root.SDMarks = api;
})(typeof window !== 'undefined' ? window : globalThis, function () {
  // existing engine code, returning the API object at the end
  return API;
});
```

Verify the engine still works in the browser harness (`app.js`).

### Phase 2 — Rasterizer

Install `@resvg/resvg-js` (Rust-based, no headless browser, no
native build dependencies on Windows).

```js
const { Resvg } = require('@resvg/resvg-js');
const svg = SDMarks.render('roundel', team, { size: 512 });
const png = new Resvg(svg).render().asPng();
```

Smoke-render to disk for visual inspection before wiring up blob
upload.

### Phase 3 — Postgres data fetch

`pg` (node-postgres). Connection string from `.env` (do NOT commit).

```sql
-- Franchises
SELECT "Id", "Sport", "Abbreviation", "DisplayNameShort",
       "DisplayName", "ColorCodeHex", "ColorCodeAltHex"
FROM "Franchise"
WHERE "IsActive" = true;

-- FranchiseSeasons (join Franchise for Sport context)
SELECT fs."Id", fs."FranchiseId", fs."SeasonYear", fs."Abbreviation",
       fs."DisplayNameShort", fs."DisplayName",
       fs."ColorCodeHex", fs."ColorCodeAltHex", f."Sport"
FROM "FranchiseSeason" fs
JOIN "Franchise" f ON f."Id" = fs."FranchiseId"
WHERE fs."IsActive" = true;
```

Map `Sport` enum integer → string (`'NFL' | 'MLB' | 'NBA' | 'NHL' | 'NCAA'`)
for the renderer. Need to read the `Sport` enum definition during
implementation to get the integer mapping right.

`Franchise.Abbreviation` is nullable; if it's null, fall back to a
2-3 char monogram derived from `DisplayNameShort` or `Name`.

### Phase 4 — Generate + upload

For each row × each direction:

```js
const team = {
  abbr: row.Abbreviation || deriveAbbr(row.DisplayNameShort),
  name: row.DisplayName,
  sport: row.Sport,
  primary: row.ColorCodeHex,
  secondary: row.ColorCodeAltHex  // may be null — engine handles it
};

for (const direction of ['roundel', 'shield', 'hex']) {
  const svg = SDMarks.render(direction, team, { size: 512, theme: 'light' });
  const png = new Resvg(svg).render().asPng();
  const blobPath = `franchise/${direction}/${row.Id}.png`;
  await containerClient.getBlockBlobClient(blobPath).uploadData(png, {
    blobHTTPHeaders: { blobContentType: 'image/png' }
  });
}
```

`@azure/storage-blob` is the Azure Node SDK. Connection string
matches the one Producer / Provider use today.

Container: `sportdeets-marks` (one container, franchise / season +
direction segments make up the blob path).

### Phase 5 — Insert logo rows

For each generated mark (3 directions × N franchises × N
franchise-seasons), INSERT into `FranchiseLogo` or
`FranchiseSeasonLogo`:

```sql
INSERT INTO "FranchiseLogo"
  ("Id", "FranchiseId", "Uri", "OriginalUrlHash",
   "Width", "Height", "Rel", "IsForDarkBg",
   "CreatedUtc", "ModifiedUtc")
VALUES
  (@id, @franchiseId, @uri, @hash,
   512, 512, ARRAY['sportdeets-mark', @direction], false,
   NOW() AT TIME ZONE 'UTC', NOW() AT TIME ZONE 'UTC');
```

Where:
- `@id` = `crypto.randomUUID()` (new Guid)
- `@direction` = one of `'roundel'`, `'shield'`, `'hex'`
- `@uri` = blob URL returned from Phase 4 (includes direction segment)
- `@hash` = `sha256("sportdeets-mark:" + direction + ":" + row.Id)`
  (synthetic, direction-scoped to avoid collisions on the indexed
  `OriginalUrlHash` column)

Pre-check: if a row with the same synthetic hash already exists,
UPDATE rather than INSERT (so reruns are idempotent — Claude Design
iteration on any direction should be safe to re-batch).

Need to verify `CanonicalEntityBase<Guid>` exact column names for
`CreatedUtc` / `ModifiedUtc` during implementation. There may also
be additional audit columns.

### Phase 6 — C# logo selection (separate PR)

The current logo selection picks rows from `FranchiseLogo` /
`FranchiseSeasonLogo`. Change the selection to prefer rows whose
`Rel` array contains BOTH `"sportdeets-mark"` AND the currently-
preferred direction (e.g. `"roundel"`).

Likely a small change in either:
- An API query handler that returns logo URLs
- A query extension method that picks the "primary" logo for a franchise

Implementation order:
1. Hard-code the preferred direction as a `const string` in the
   selection code (one-line swap to change)
2. Promote to Azure AppConfig as a fast follow-up if we end up
   switching directions during friend-tester phase
3. Per-user preference is deferred — only revisit if friend testers
   actually disagree on direction

Need to grep for the current selection logic during implementation.
Memory note: FranchiseSeason logo is preferred over Franchise logo
as fallback — keep that ordering, just change which row within each
table wins.

## File layout

```
src/marks/
  marks.js                       # engine (UMD-shimmed in Phase 1)
  app.js                         # browser demo harness (unchanged)
  index.html                     # browser demo (if/when added)
  batch-generation-plan.md       # this doc
  batch/
    package.json                 # Node deps scoped to this folder
    package-lock.json
    .env.example                 # connection-string templates
    .env                         # local secrets — gitignored
    generate.js                  # the batch script (Phases 1-5)
    .gitignore                   # excludes .env, node_modules
```

`src/marks/batch/.gitignore` and the root `.gitignore` together
must keep `.env` and `node_modules` out of git. Verify before first
commit.

`package.json` for the batch scoped to `src/marks/batch/` so Node
dependencies don't bleed into the .NET solution structure or get
picked up by EAS builds / Azure Pipelines.

Dependencies:
- `pg` — Postgres driver
- `@azure/storage-blob` — Azure Blob SDK
- `@resvg/resvg-js` — SVG → PNG rasterizer
- `dotenv` — `.env` loader

## What we need before starting

- Local Postgres copy of prod data (or willingness to start with
  the dev environment's franchise list, which is smaller but
  validates the pipeline)
- Azure Blob connection string for the test environment, placed
  in `src/marks/batch/.env` (NEVER commit)
- Confirmation on the recommendations in the Decisions section
  (PNG, 512px, new rows tagged `sportdeets-mark`, single container,
  skip light/dark variants, run against local first)

## Open follow-ups (deferred)

- **SVG output path.** When mobile / web both have SVG renderers
  wired up, regenerate as SVG for crispness at every size.
- **Light/dark variants.** Generate the `theme: 'dark'` variant and
  tag rows with `IsForDarkBg = true` if real contrast issues
  surface.
- **Wire into Producer ingest.** When a new Franchise or
  FranchiseSeason is sourced, auto-generate the mark instead of
  needing a manual rerun of this batch script. Low priority —
  new franchises are rare, manual rerun is fine until it isn't.
- **Multi-size output.** Pre-render 24/64/256/512 if download size
  or visual quality at thumbnail sizes becomes an issue.
- **Cleanup of ESPN-sourced rows.** Once UI confirms the
  `sportdeets-mark` rows render everywhere correctly, drop the
  ESPN rows from `FranchiseLogo` / `FranchiseSeasonLogo`. Keep them
  for at least one full release cycle as a revert path.

## Related docs

- `docs/team-mark-design-brief.md` — the design problem statement
  and Claude Design brief
- `marks.js` — the engine
- `app.js` — browser playground for visual comparison
