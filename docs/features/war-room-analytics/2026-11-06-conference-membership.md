# Historical conference membership: root cause and remedy options

**Status:** Shipped in PR #857 (merged 2026-11-06); backfill done 2026-11-07 (2001-2025), 4 residual team-seasons (see [Rollout log](#rollout-log))
**Scope:** NCAA football (sdProducer.FootballNcaa), seasons 2000-2025
**Resolves:** Finding 3 in [2026-11-01-coverage-audit.md](2026-11-01-coverage-audit.md)
**Gates:** Phase 2 (anything that splits by conference or uses conference W/L)

All queries ran against Production in read-only sessions. ESPN data came from
the public core API (`sports.core.api.espn.com`). Nothing was written.

## Summary

229 team-seasons between 2001 and 2020 carry the wrong conference, the
wrong FBS/FCS division, or both. The cause is ESPN's per-team season document,
which links each team to its current-era conference even for old seasons. We
store that link as served. ESPN's per-conference member lists are correct, so
the fix can be driven entirely by data ESPN publishes, with no hand-curated
table. 2013-2025 is clean except two 2020 teams. 2000 cannot be verified.

Decided: fix in code by sourcing conference member lists (see
[Design](#design)). Season 2000 stays unverified (see [Season 2000](#season-2000)).

## Root cause

`FranchiseSeason.GroupSeasonId` is set once, when the team-season is created,
by `TeamSeasonDocumentProcessor.ProcessDependencies`: it resolves the TeamSeason
document's `groups` $ref against `GroupSeasonExternalId.SourceUrlHash`.
`ProcessUpdateEntity` only processes children and never re-resolves it.

The `groups` $ref is wrong at the source. Live ESPN documents:

| Document | `groups` $ref | Resolves to | 2005 reality |
|---|---|---|---|
| `seasons/2005/teams/2628` (TCU) | `seasons/2005/types/3/groups/4` | Big 12 | Mountain West |
| `seasons/2005/teams/30` (USC) | `seasons/2005/types/3/groups/5` | Big Ten | Pac-10 |

The conference side of the relationship is honest. Live ESPN member lists,
`seasons/2005/types/2/groups/{conf}/teams`:

| Group | Count | Contains |
|---|---|---|
| 4 (Big 12) | 12 | not TCU (2628) |
| 17 (Mountain West) | 9 | TCU (2628) |
| 9 (Pac-10) | 10 | USC (30) |
| 5 (Big Ten) | 11 | not USC |

ESPN labels the groups with current-era abbreviations (`pac12` for the
Pac-10 era), but membership follows the season.

`FranchiseSeason.GroupSeasonMap` (e.g. `NCAAF|NCAA|fbs|sec`) is not written by
any service. It was built once from the `GroupSeasonId` -> `ParentId` chain by
the manual script `sql/pgsql/groupSeasonHierarchy.sql`, so it inherits the same
error. Production code that reads it: the FBS filter in
`GetCompletedFbsContestIds.sql`, the matchup SQL, `GetContestOverview`,
`GetAthleteMatchupSummaries`, and the metrics conference-parent derivation.
`EnrichFranchiseSeasonHandler` computes conference W/L by comparing
`GroupSeasonId` between the two teams, so conference records are wrong for
every affected team-season.

## Method

1. Pulled ESPN membership for every FBS (`groups/80`) and FCS (`groups/81`)
   conference: `children` of each division, then each conference's `teams`
   index, regular season (`types/2`), 2000-2025. 52 passes, 651 conference-seasons,
   about 1,350 requests at 1 per second.
2. Exported every prod `FranchiseSeason` 2000-2025 with its ESPN team id
   (from `FranchiseSeasonExternalId.SourceUrl`), the ESPN group id it resolved
   to, `GroupSeasonMap`, and its contest count.
3. Compared at conference level: a team-season matches when its resolved
   group, or the conference segment of its map, equals the ESPN conference
   whose member list contains it. Division-level groups (e.g. `beltw`) count
   as their conference. Team-seasons with no contests are excluded.

## Results

Per season, team-seasons with at least one contest:

| Season | Match | Wrong conference | Wrong division | FBS in prod, not in ESPN lists |
|---|---|---|---|---|
| 2001 | 206 | 31 | 3 | 0 |
| 2002 | 203 | 32 | 3 | 0 |
| 2003 | 204 | 32 | 3 | 0 |
| 2004 | 201 | 32 | 3 | 0 |
| 2005 | 226 | 13 | 0 | 0 |
| 2006 | 228 | 13 | 0 | 0 |
| 2007 | 229 | 13 | 0 | 0 |
| 2008 | 232 | 13 | 0 | 0 |
| 2009 | 232 | 13 | 0 | 1 |
| 2010 | 219 | 13 | 0 | 1 |
| 2011 | 242 | 4 | 0 | 0 |
| 2012 | 242 | 4 | 0 | 0 |
| 2013-2019 | all | 0 | 0 | 0 |
| 2020 | 245 | 2 | 0 | 0 |
| 2021-2025 | all | 0 | 0 | 0 |

Totals: 215 wrong conference, 12 wrong division (prod FBS, ESPN FCS), 2 FBS in
prod with no ESPN membership. 229 team-seasons, 41 team spans (listed in the
[appendix](#appendix-affected-team-seasons)).

Patterns:

- **Pac-10 (2001-2010):** all ten members except Oregon State and Washington
  State carry their current conference (Big Ten, Big 12, ACC).
- **Mountain West (2001-2012):** TCU, BYU, Utah, SDSU, Colorado State,
  Boise State, Fresno State carry their current conference.
- **C-USA (2001-2004):** the schools that later left (Louisville, Cincinnati,
  Houston, Memphis, USF, UAB, Tulane, East Carolina, Army, Southern Miss, TCU)
  carry their current conference.
- **Atlantic 10 football (FCS, 2001-2004):** members carry CAA, Patriot, or no
  conference; Delaware, UMass, and James Madison are also tagged FBS.
- **2020:** FAMU and Bethune-Cookman are tagged SWAC; ESPN lists them in the
  MEAC (they moved for 2021).
- **South Alabama 2009-2010:** tagged FBS Sun Belt; ESPN lists them in no
  FBS or FCS conference (transition years).

## Verified and not verified

Verified:

- The ESPN TeamSeason `groups` $ref and the member lists above, live.
- `ProcessUpdateEntity` does not touch `GroupSeasonId`, so a correction to the
  column would not be reverted by a re-process of the same document.
- `GroupSeason` rows exist for the true conference in a sampled year (2005
  Mountain West, all four season-type variants).

Not verified:

- **2000:** ESPN returns no groups and no member lists for 2000
  (`seasons/2000/types/2/groups` and every `groups/{id}/teams` return
  `count: 0`). 2000 membership cannot be checked against ESPN.
- **Divisions within a conference** (e.g. SEC East vs West): the comparison is
  conference-level only.
- **Teams outside FBS/FCS** (Division II, III, NAIA): not in the pulled lists;
  not checked. They do not affect FBS analytics.
- **That a `GroupSeason` row exists for every target conference-year** the
  correction would need. Checked in one sample only; the remediation script
  must verify all targets before it writes.

## Related data-model quirk

`GroupSeason` stores each group once per season type (`types/1` to `types/4`),
four rows per conference per season; this is also the source of the "4 FBS
rows per year" counts in `groupSeasonHierarchy.sql`. Each season's team-seasons
link to a single variant consistently: `types/3` for 2000-2024, `types/2` for
2025 (all 797). Conference equality therefore holds within a season today. Any
correction must target the variant that season already uses, or conference W/L
breaks between corrected and uncorrected teams.

## Decision (2026-11-06)

Fix in code, no data script. Membership comes from sourcing group seasons: a
conference's (or division's) member list is authoritative, and the TeamSeason
document's `groups` link is a fallback for seasons with no lists. Existing
data heals by re-sourcing group seasons. No new table and no data script.

## Design

### Spot check: other leagues

NFL looks honest in the one sample checked: `nfl/seasons/2001/teams/26`
(Seattle) links `types/2/groups/6`, the AFC West, and that group's member list
contains Seattle. Seattle moved to the NFC West in 2002, so this is the case
that would show fabrication. MLB is unchecked. Run the same comparison for NFL
and MLB before rollout; the design does not depend on the result, because
member lists win wherever they exist.

### 1. No new table: `FranchiseSeason.GroupSeasonId` is the membership

Decided 2026-11-06. The member list reaches us the way Provider delivers every
resource index: Provider walks the `teams` index and publishes one document
message per entry. Each entry is the team's TeamSeason document
(`seasons/{y}/teams/{id}`), and the message carries the GroupSeason's id as
`ParentId`. So the authoritative group arrives *with* the team-season, and
`TeamSeasonDocumentProcessor` writes it straight to `GroupSeasonId`. Arrival
order stops mattering: whichever path creates the team-season, the group
fan-out sets the column when it lands. Same parent-corroboration pattern
`AthleteSeasonDocumentProcessor` uses for roster fan-out.

### 2. `GroupSeasonDocumentProcessor` follows its `teams` index

- `EspnGroupSeasonDto.Teams` already exists (the processor ignored it). ESPN
  serves it on every group document checked (NCAA 2005 group 4, both
  `types/2` and `types/3`; NFL 2001 group 6).
- In `ProcessChildren`, publish `dto.Teams` as a child request of type
  `DocumentType.TeamSeason` with this GroupSeason as parent. This replaced the
  "Teams: Team roster by conference/division" note.
- **Leaf groups only.** ESPN serves no `children` link on leaf groups (2005
  Mountain West, 2011 Pac-12 South, 2025 SEC); the 2011 Pac-12 and FBS have
  one. The member list is followed only when `Children` is null, so a team
  arrives once per run, from its most specific group.
- **Document-only (implemented).** The fan-out carries an empty
  `IncludeLinkedDocumentTypes`
  (`GroupSeasonDocumentProcessor.MembershipLinkedDocumentTypes`), enforced at
  the publish choke point even for new entities, so a list never cascades
  into logos, rosters, or schedules. A team-season the list *creates* is bare
  but correctly grouped; ordinary sourcing hydrates it later (an update with a
  null filter spawns children) and never overwrites the group. This also
  bounds the cost of Division II/III lists, whose teams we may never have
  sourced. `PublishChildDocumentRequest` gained an optional per-call filter
  (narrow only) to carry it.
- Provider's historical republish cooldown suppresses an identical document
  republished within the window when the filter is null or empty. Each team
  appears in one leaf list per run, so a single run is unaffected; re-running
  the same season inside the window is a no-op for teams already delivered.

### 3. `TeamSeasonDocumentProcessor`: parent group wins, link as fallback

- If `command.ParentId` resolves to a `GroupSeason`, that group is
  authoritative: set `GroupSeasonId` to it, on **both** the new and the
  existing path (`ProcessUpdateEntity` today never touches `GroupSeasonId`).
  Log old and new values when it changes.
- Otherwise (no parent, or the parent is not a GroupSeason) resolve
  `dto.Groups` as today, and only on create. An existing value is never
  overwritten by the document link.
- **Most specific group wins.** Do not replace a `GroupSeasonId` with any of
  its ancestors (`GroupSeasonHierarchy.IsAncestorAsync`). Leaf-only fan-out
  already avoids this in one run; the guard covers lists from other paths.
- **Season-type variants.** `GroupSeason` has one row per season type (see
  [Related data-model quirk](#related-data-model-quirk)), and every team-season
  in a season must point at the same variant or conference W/L equality breaks.
  Rule: if the team-season already points at a variant, map the incoming group
  to the same `types/{n}` row for that ESPN group id and season; otherwise use
  the incoming variant (`GroupSeasonHierarchy.MatchVariantAsync`). The backfill
  also seeds only the variant the season uses.
- Log which source set the group (parent or link) so fallbacks are countable
  in Seq.

### 4. Move `groupSeasonHierarchy.sql` into the document processors

`GroupSeasonMap` is written only by the manual script
`sql/pgsql/groupSeasonHierarchy.sql` (confirmed 2026-11-06). The script does
three things:

1. Patches FBS/FCS `GroupSeason.Abbreviation` from `UNK` to `fbs`/`fcs`,
   matched on `Name = 'FBS'`/`'FCS'` (some lines pinned to row ids).
2. Builds each group's path with a recursive CTE: root (`ParentId IS NULL`)
   down through `ParentId`, abbreviations joined with `|`
   (`NCAAF|NCAA|fbs|sec`).
3. Writes that path to `FranchiseSeason.GroupSeasonMap` for every team-season
   with a group. The `UPDATE` is commented out and run by hand; it rewrites all
   seasons, so it is idempotent.

Prod state (read-only, 2026-11-06):

- 16 team-seasons have a group but no map. Re-running the `UPDATE` changes
  exactly those 16; every other map already equals the computed path.
- The 2026 FBS and FCS rows (created 2026-08-31) still carry `UNK`. No current
  map contains `UNK`, but the next run would write `NCAAF|NCAA|UNK|...` for any
  team-season under them, silently dropping it from every `|fbs` filter.
- The name match is brittle: a division named other than exactly `FBS`/`FCS`
  stays `UNK`. 2025's "FBS (I-A)" only works because ESPN supplied `fbs`.

Move each step to the processor that owns the data:

- **Step 1 -> `GroupSeasonDocumentProcessor`** (`GroupSeason.AsEntity` or the
  processor, on create and in `HandleExisting`): when ESPN's abbreviation is
  missing or `UNK`, derive it. FBS/FCS: map ESPN group ids 80/81 to `fbs`/`fcs`
  rather than matching on `Name` (NCAA football only; ids are per league; 80/81
  held for every season pulled, 2001-2025). Other groups keep `UNK` as
  today (`GroupSeasonHierarchy.ResolveAbbreviation`). Tested explicitly;
  `GetCompletedFbsContestIds.sql` and the matchup queries filter on `|fbs`.
- **Steps 2-3 -> `TeamSeasonDocumentProcessor`**, wherever it sets
  `GroupSeasonId` (item 3): walk `ParentId` to the root and set
  `GroupSeasonMap` in the same save. The chain is always complete at that
  point: `GroupSeasonDocumentProcessor` refuses to create a group until its
  parent exists (it throws `ExternalDocumentNotSourcedException` and retries),
  so any group a team-season can resolve already has a full path. A shared
  helper (`GroupSeasonHierarchy.BuildMapAsync`) builds the path. A missing
  map is also filled whenever a team-season is processed.
- **Abbreviation corrections after the fact:** if `HandleExisting` changes a
  group's abbreviation, maps below it go stale. Recompute `GroupSeasonMap` for
  team-seasons under that group in the same handler.
- **Existing rows:** the backfill (item 6) re-sources every season's groups,
  which fixes abbreviations and, through the `teams` fan-out, rewrites every
  team-season's group and map. That covers the 16 missing maps and the 2026
  `UNK` rows.
- **Retire the script** after the backfill verifies: delete the `UPDATE` and
  `Abbreviation` patches from `groupSeasonHierarchy.sql` (or the file),
  leaving at most the read-only path query for debugging.

### 5. Implement `HandleExisting` in `GroupSeasonDocumentProcessor`

Implemented. It resolves an `UNK` abbreviation (refreshing maps below it) and
re-runs `ProcessChildren`. Children and the member list are walked only when
the command's filter **names** the type (`GroupSeason`, `TeamSeason`): an
ordinary re-hit with a null filter stays a no-op, as before, because walking
the subtree and every member list on each re-hit would re-process every team
in the conference. Other group fields are not updated.

### 6. Backfill: re-source group seasons

New endpoint, under the `franchise-seasons` family the ops proxy already
allowlists (no API change):

`POST {{baseUrlApi}}/api/ops/producer/football/ncaa/franchise-seasons/seasonYear/{year}/conferences/source`

(Producer route `POST api/franchise-seasons/seasonYear/{year}/conferences/source`,
`RequestConferenceMembershipSourcingCommandHandler`.) It determines the
season-type variant the season's team-seasons use, then publishes that
variant's root group(s) (ESPN `groups/99` "NCAA Football", plus NAIA where
present) with the filter `[GroupSeason, TeamSeason]`, so existing groups walk
their children and member lists. Returns the correlation id synchronously
(a handful of publishes); NotFound when no team-season in the season has a
group.

After deploy, run it for NCAA 2001-2025, one season at a time. Each run walks the
whole tree from the root, Division II/III included; FBS and FCS alone are
about 25 leaf groups and 260 team-season documents per season (from the
2026-11-06 pull), the full tree size is not measured. Then rerun
records enrichment for 2001-2012 and 2020 (ops proxy,
`franchise-seasons/seasonYear/{year}/enrich`) so conference W/L is recomputed.

### 7. Verification

- Unit tests: GroupSeason processor publishes the `teams` index document-only;
  `HandleExisting` re-runs children; TeamSeason processor with a GroupSeason
  parent (new and existing), without one (link fallback, create only),
  division-over-conference, variant rule; computed map including the `UNK`
  division case.
- Local E2E per the no-commit-before-local-E2E rule: source 2005 TCU and USC
  from scratch in both orders (group first, team-season first).
- Prod after backfill: rerun the ESPN-vs-prod comparison (expect 0 mismatches
  2001-2025) and coverage audit section 1 (`pct_conf_map` shifts for
  2001-2012).

### Out of scope

- Collapsing the four season-type `GroupSeason` variants into one.
- Division-level correctness inside a conference beyond what
  most-specific-wins gives (not measured).
- Teams outside FBS/FCS lists (Division II, III, NAIA).

## Rollout log

### PR #857 review follow-ups

The re-source endpoint returns 404 (no correlation id) in two no-work cases:
no team-season in the season has a group, and no sourced root group exists
for the season-type variant in use. When team-seasons split evenly across two
variants, the tie goes to a variant with a sourced root, then to the lowest
type; the majority variant still wins outright, root or not.

### 2005 test (2026-11-06)

Correlation id `a0c7d578-4727-3dc7-94c8-bdd72cfdae35`. The run took about two
minutes (21:49:52 to 21:51:47 UTC), nothing above Information in Seq.

- Seq logged 13 "GroupSeason corrected from member list" lines, matching the
  13 wrong-conference rows in [Results](#results). BYU and TCU moved to the
  Mountain West, Arizona State and Stanford to the Pac-12.
- The member-list fan-out stayed document-only: each TeamSeason logged
  "Skipping spawn of {type}: empty inclusion filter" for every child type.
- Comparison afterwards: 239 match, 0 wrong conference, 0 wrong division.

### 2001-2004, 2006-2009 (2026-11-06)

| Season | Correlation id |
|---|---|
| 2001 | `d4737cee-4153-6b91-cb64-312b5a20e08e` |
| 2002 | `f5a6dc61-f9e4-9d12-ffdc-48057c3e05d7` |
| 2003 | `3f9912d7-3a55-a549-d291-d3f89d4d8469` |
| 2004 | `d0754fb4-692d-80aa-4dde-50862dbb393a` |
| 2006 | `00f193d4-c76b-7034-98f8-e3426aaec72c` |
| 2007 | `b4438e88-f358-8eb2-780b-61fd300bc47b` |
| 2008 | `de708da7-d72d-3883-0556-9d271f0a0b9c` |
| 2009 | `e9c190e9-5a85-9462-16e4-757201882f54` |

All eight finished by 22:19 UTC with nothing above Information in Seq. The
comparison ran at 22:25 UTC, same method as [Results](#results):

| Season | Match | Wrong conference | Wrong division | FBS in prod, not in ESPN lists |
|---|---|---|---|---|
| 2001 | 240 | 0 | 0 | 0 |
| 2002 | 238 | 0 | 0 | 0 |
| 2003 | 239 | 0 | 0 | 0 |
| 2004 | 236 | 0 | 0 | 0 |
| 2005 | 239 | 0 | 0 | 0 |
| 2006 | 241 | 0 | 0 | 0 |
| 2007 | 242 | 0 | 0 | 0 |
| 2008 | 245 | 0 | 0 | 0 |
| 2009 | 245 | 0 | 0 | 1 |

Each season's new match count equals its old match + wrong conference + wrong
division, so every previously wrong row was corrected and none regressed.

### 2010-2019 (2026-11-07)

| Season | Correlation id |
|---|---|
| 2010 | `b66c2229-fb49-cae7-46d7-d5eaf48fa21a` |
| 2011 | `7c534cd4-38a6-f847-ab4d-eeb986b35ce5` |
| 2012 | `1acf42af-6706-0eba-59fc-42c5f3f7aaaf` |
| 2013 | `89091097-afcc-de3f-9f27-6fed0a09c34a` |
| 2014 | `9c844e63-62f7-b081-fbcf-6de54077e535` |
| 2015 | `66b21589-a9b0-6621-e2c8-a521c714d1bd` |
| 2016 | `0e8d3216-40be-15c1-5082-18c13c8fcd75` |
| 2017 | `bfc2b56e-b6bc-0f38-a062-021659986043` |
| 2018 | `9aa29984-5d71-a091-a7ce-a1f28de50cbb` |
| 2019 | `944efdd7-1685-b571-cbbb-7dc2a9c073a4` |

All ten finished by 10:35:36 UTC with nothing above Information in Seq. The
comparison ran at 10:42 UTC:

| Season | Match | Wrong conference | Wrong division | FBS in prod, not in ESPN lists |
|---|---|---|---|---|
| 2010 | 232 | 0 | 0 | 1 |
| 2011 | 246 | 0 | 0 | 0 |
| 2012 | 246 | 0 | 0 | 0 |
| 2013 | 252 | 0 | 0 | 0 |
| 2014 | 256 | 0 | 0 | 0 |
| 2015 | 253 | 0 | 0 | 0 |
| 2016 | 253 | 0 | 0 | 0 |
| 2017 | 254 | 0 | 0 | 0 |
| 2018 | 255 | 0 | 0 | 0 |
| 2019 | 256 | 0 | 0 | 0 |

2010-2012 again equal old match + wrong conference + wrong division. 2013-2019
were already clean and stayed clean. The 2010 outlier is South Alabama.

### 2020-2025 (2026-11-07)

| Season | Correlation id |
|---|---|
| 2020 | `8757745b-2a9b-ba66-1d1f-4ea6566b3a85` |
| 2021 | `5a543f43-2648-c9d9-2f1b-ef42790b20fb` |
| 2022 | `6ed8fdc3-5327-6d54-f2cc-20b3e169cfe7` |
| 2023 | `0cad8d14-806b-cf7c-c2d7-3b4aeb9a1373` |
| 2024 | `68b78a89-897b-ee60-800e-6c010c5ad62a` |
| 2025 | `1d14d99f-4bb7-54f6-3f4f-743dea7d3743` |

All six finished by 10:46:47 UTC with no errors. The only warnings were 38 on
the 2023 run, all Provider "Cached document has null/empty data, falling back
to ESPN fetch" on GroupSeason documents: the Mongo copy was empty and Provider
fetched ESPN live, so the walk was not interrupted (consistent with the
historical NCAA Mongo cache gap). 2023 compares clean.

Full comparison, 2001-2025, at 10:54 UTC:

| Season | Match | Wrong conference | Wrong division | FBS in prod, not in ESPN lists |
|---|---|---|---|---|
| 2001-2019 | as in the tables above | 0 | 0 | 2009: 1, 2010: 1 |
| 2020 | 245 | 2 | 0 | 0 |
| 2021 | 258 | 0 | 0 | 0 |
| 2022 | 261 | 0 | 0 | 0 |
| 2023 | 261 | 0 | 0 | 0 |
| 2024 | 263 | 0 | 0 | 0 |
| 2025 | 265 | 0 | 0 | 0 |

2025 (the one season on `types/2`) compares clean, so the variant selection
works for both variants.

Residual after the backfill: 4 team-seasons in 25 seasons. South Alabama
2009-2010 (below) and the two 2020 MEAC rows (below).

### Records re-enrichment (2026-11-07)

Ran `POST {{baseUrlApi}}/api/ops/producer/football/ncaa/franchise-seasons/seasonYear/{year}/enrich`
(Bruno: `bruno/api/franchise-seasons-enrich-seasonYear.yml`) for 2001-2012
and 2020. No warnings or errors in Seq.

| Season | Correlation id |
|---|---|
| 2001 | `6a216535-a492-4d51-a6ba-66b45b7c3bef` |
| 2002 | `308d31d0-bdd1-46e6-8ef3-e989ae90ba06` |
| 2003 | `4c9b688c-d16a-4dc6-82e5-9e33c361efb2` |
| 2004 | `67df83c7-76fb-4991-ae58-1b308119bb9e` |
| 2005 | `43ae151a-7919-4bcc-b0f2-002f7448f0ec` |
| 2006 | `ab7a6a9d-db32-48a9-aa97-90aefc13a1ea` |
| 2007 | `9a40be27-3648-4f11-aa44-dfe77c70cc07` |
| 2008 | `93541fc0-c273-41f0-bce6-84ba06b722fb` |
| 2009 | `d14645cd-6106-4584-9135-dab546ed356e` |
| 2010 | `fac53691-4a87-4174-8c95-55496f7d9b50` |
| 2011 | `4d0eaf6b-87d6-4305-a12e-5b90fb1b8c74` |
| 2012 | `a43edf50-8bd0-4a91-a995-2556371dcdc6` |
| 2020 | `9d86c8b7-98a1-49b9-8741-28b83798afb9` |

Verification (read-only): every team-season with a finalized non-preseason
game in those 13 seasons was re-enriched today; 0 overall W/L mismatches
(coverage audit section 5) and 0 conference W/L mismatches, recomputing the
handler's rule (opponent shares `GroupSeasonId`) in SQL. Spot checks match
the real records: Louisville 2004 11-1 (C-USA 8-0), BYU 2005 6-6 (MWC 5-3),
USC 2005 12-1 (Pac-10 8-0), Utah 2008 13-0 (MWC 8-0). TCU 2010 shows 1-0
(real: 13-0, MWC 8-0) because most 2010 contests are missing; 2010 has 236
team-seasons with finals against about 690 in 2009 and 2011. That is the
separate 2010 coverage gap, not membership.

### `ModifiedUtc` fix (2026-11-07, PR #858, merged)

`TeamSeasonDocumentProcessor` now stamps `ModifiedUtc` (via
`IDateTimeProvider`) and `ModifiedBy` (the command's correlation id) on the
update path when the member list changes `GroupSeasonId` or `GroupSeasonMap`,
the only columns that path writes. An unchanged re-process does not stamp, so
`ModifiedUtc` keeps meaning "this row changed", the codebase convention
(`TeamSeasonRecordDocumentProcessor` and others). Coverage audit section 5's
`never_enriched` now reads `W+L+T = 0 despite finals` instead of
`ModifiedUtc IS NULL`.

### 2010 game coverage (2026-11-07)

Found while checking the re-enrichment: TCU 2010 showed 1-0. Measured
read-only across all divisions:

| Season | Contests | No `Competition` | Finalized |
|---|---|---|---|
| 2009 | 3,546 | 0 | 3,542 |
| 2010 | 2,760 | 484 | 298 |
| 2011 | 3,619 | 3 | 2,193 |

- **About 800 contests missing.** 2010 runs at about 78% of 2009 in every
  week, not one missing week. 205 team-seasons have 1-5 games (2009: 46,
  2011: 52) and 48 have none (2009: 30), so team schedules (the TeamSeason
  `events` index) were only partly walked.
- **1,978 `Competition` rows are empty shells:** no competitors, no scores,
  no status. Only the 298 finalized games are complete. ESPN serves the full
  data (sampled `events/302540083/competitions/302540083`: two competitors,
  status, score links), so this is lost sourcing, not missing source data.
- **Cause (inferred, not verified):** every 2010 contest was created on
  2026-03-22, one sourcing run. `EventCompetitionDocumentProcessorBase` saves
  the `Competition` row first and publishes its status and competitors
  afterwards, committed at the tail save, so a run whose child messages were
  lost leaves exactly these shells. Seq does not reach back to March to
  confirm.
- **The season contest refresh cannot fix the shells:**
  `RefreshContestsBySeasonYearHandler` finds contests through
  `CompetitionCompetitor`, which the shells lack.

Plan: re-source from the team-seasons with a narrow filter, then finalize and
enrich.

1. `POST {{baseUrlApi}}/api/football/ncaa/franchises/seasons/2010/source`
   (Bruno: `bruno/api/sourcing-franchise-season-games.yml`) with
   `[Event, EventCompetitionStatus, EventCompetitionCompetitor,
   EventCompetitionCompetitorScore, EventCompetitionCompetitorLineScore,
   EventCompetitionPlay]`.
   The filter is inherited downhill; the Event processor always publishes its
   competition; an existing competition publishes status and competitors
   only when named. Missing contests are new, so they get their full subtree
   (`isNew` bypass). Rosters, team stats, and the rest of the TeamSeason tree
   are skipped. A non-empty filter is not suppressed by Provider's
   historical cooldown.
2. Finalize 2010 contests
   (`api/ops/producer/football/ncaa/contests/seasonYear/2010/finalize`).
3. Re-enrich 2010 records
   (`api/ops/producer/football/ncaa/franchise-seasons/seasonYear/2010/enrich`).
4. Verify: contest count near 2009/2011, no empty competitions, finalized
   count, coverage audit section 5, TCU 2010 13-0.

Plays are included (decided 2026-11-07: a one-time ESPN hit, done now; 2009
has plays for 1,232 games, 2010 for 211). Play participants request missing
athletes and positions document-only (FK only, no headshots or season
stats). Per-play athlete statistics need the game's roster documents
(`EventCompetitionCompetitorRoster`), which are not in the filter, so they
are skipped.

**Re-source result.** Correlation id `64e7b019-3110-4287-8191-426e001b2e70`,
started 11:49 UTC, settled 14:05 UTC (no change over the next 20 minutes).

| Season | Contests | No `Competition` | Empty `Competition` | `STATUS_FINAL` | With plays | Finalized |
|---|---|---|---|---|---|---|
| 2009 | 3,546 | 0 | n/a | 3,543 | 1,232 | 3,542 |
| 2010 before | 2,784 | 508 | 1,978 | 298 | 211 | 298 |
| 2010 after | 3,571 | 0 | 0 | 3,569 | 1,223 | 3,569 |
| 2011 | 3,619 | 3 | n/a | 3,610 | 1,297 | 2,193 |

- The two non-final 2010 games are `STATUS_CANCELED` (Minnesota-Morris at
  Crown, Martin Luther at Northwestern (MN)).
- Every contest was finalized during the run, as each game's status went
  final, so step 2 (season finalize) has nothing left to do.
- The play processor's "Competition status not yet sourced" exceptions in Seq
  are its designed retry when a play arrives before its game's status.
  Sampled competitions all ended with status, competitors and plays.

**Wrong winners from finalizing mid-run.** Spot checks failed: TCU 11-1 with
a tie (real 13-0), Auburn 13-1 (14-0), Boise State 13-0 (12-1). The scores are
right; `WinnerFranchiseSeasonId` is not. In 2010, 85 finalized contests have
a winner that contradicts the score (59 wrong team, 26 null).

- **Mechanism.** `FootballContestEnrichmentProcessor` takes the final score as
  MAX over scoring plays. It ran as soon as status went final, while that
  game's plays were still arriving, and wrote a partial score and winner
  (finalized about 12:08). The full score then landed through
  `CompetitorScoreUpdatedConsumerHandler` (about 12:30), which writes
  `HomeScore`/`AwayScore` on a finalized contest without re-deriving the
  winner. Example: Boise State at Nevada is stored 34-31 Nevada with Boise
  State as the winner.
- **Season finalize with `ReprocessEnriched` does not fix it.** It re-enqueues
  enrichment, but the processor returns early for an already-finalized
  contest. Only the per-contest re-enrich clears the derived fields first.
- **The audit job would catch them, but not soon.** All 85 have
  `AuditedUtc IS NULL` and none is flagged. The audit sweep takes 500 per
  firing (00/06/12/18 UTC), newest-finalized first, and 27,001 contests are
  pending. The 2010 ones sit at positions 741-3,092. Recent firings audited
  far fewer than 500 (21, 8, 178); not investigated.
- **Remedy for 2010:** re-enrich the 85 per contest
  (`bruno/api/reenrich-contest.yml` through the Collection Runner with
  `bruno/api/reenrich-contests-2010.csv`), then re-enrich 2010 records.
  Run the request through the Collection Runner with the CSV; sent alone,
  the unresolved `{{contestId}}` is URL-encoded and the ops proxy refuses
  the `%` path with a 404. Tested 2026-11-08 on Boise State at Nevada
  (correlation id `c3f45ca3-a962-c942-2312-4046c7d46151`): score re-derived
  from scoring plays, Nevada the winner, and `ContestFinalized` re-enriched
  both team records within 0.2 s (Boise State 12-1, Nevada 13-1), so the
  season records enrich afterwards is only a safety net.
- **Done 2026-11-08.** The other 84 re-enriched (all HTTP 200). 2010 now has
  0 contradicting winners among 3,569 finalized; TCU 13-0, Auburn 14-0, Boise
  State 12-1, Oregon 12-1, Stanford 12-1; W/L and conference W/L totals match
  finalized contests for all 690 team-seasons (123 re-enriched by the
  `ContestFinalized` cascade).

**Play-derived scores are wrong when ESPN's play-by-play is incomplete.**
`FootballContestEnrichmentProcessor` takes MAX over scoring plays and uses
MAX competitor score (`CompetitionCompetitorScores`) only when a game has no
scoring plays. Historical play-by-play is often partial, so the stored score
undercounts. Finalized contests whose score differs from MAX competitor score:
2008: 47, 2009: 59, 2010: 16, 2011: 120, 2012: 289 (other years not checked).
Five 2010 games are stored as ties, e.g. USC at Washington State 7-7 (competitor
score and real result: USC 50-16). The audit expects the competitor score, so
it re-queues enrichment, which re-derives the same play score, until the
contest is flagged. Proposed: prefer the competitor score when present, plays
as fallback (or MAX of both per side), then re-enrich the affected contests.
Fixed in PR #859 (merged and deployed 2026-11-08): competitor score first,
scoring plays when competitor rows are missing or 0-0 bootstrap.

**Re-enrich after #859 (2026-11-08).** Read-only measure across 2001-2025:
1,016 contests with a stored score different from MAX competitor score, 4,815
with a winner contradicting it, 5,243 with a null winner and unequal stored
scores; 6,259 distinct, none referenced by a `UserPick`. All 6,259 re-enriched
per contest through the ops proxy (all HTTP 200; one transient no-response,
retried). Afterwards: 0 score mismatches, 0 null winners, 1 contradicting
winner (Mass Maritime at Bridgewater State 2025, stored 20-7 with the away
team as winner; likely the competitor home/away re-designation quirk).

- **550 contests left unfinalized with no score** (2016: 46, 2018: 504). They
  had no competitor score rows, no scoring plays and no line scores; their old
  scores (with null winners) came from an earlier code path. Re-enrich clears
  derived fields first and found no source, so they now drop out of records.
  The list should have excluded contests with no score source. ESPN serves
  their scores (Adams State at Colorado Mesa 2018: 63-64), so the fix is a
  season contest refresh narrowed to `EventCompetition`,
  `EventCompetitionCompetitor`, `EventCompetitionCompetitorScore`,
  `EventCompetitionCompetitorLineScore` for 2016 and 2018, then season
  finalize for both (the enrichment sweep is bounded to 14 days).
  - 2026-11-09 refresh, first attempt (2018 `b52b8d15-910c-7a20-32bf-5fcc63093f55`,
    2016 `19d321f0-d15c-3446-e5c9-ee716ac7be92`): the command carried only
    `EventCompetition`, `EventCompetitionCompetitor`,
    `EventCompetitionCompetitorLineScore` (Producer logs "Skipping spawn of
    EventCompetitionCompetitorScore due to inclusion filter"). No competitor
    score rows were written; needs a re-run that includes
    `EventCompetitionCompetitorScore`. The API passes the list through as
    sent.
  - The gap is wider than the 550: before this refresh, 2018 had 2,012 and
    2016 had 70 contests with `STATUS_FINAL`, no score and no `FinalizedUtc`.
    The rest predate the re-enrich. The same refresh plus finalize covers them.
  - Second attempt with `EventCompetitionCompetitorScore` (2018
    `3a6eaf60-5b3d-9601-f737-c8537469a295`, 2016
    `36a1ec6a-8b04-4d7a-eda2-6ba4827326eb`): all 2,082 (2016: 70, 2018:
    2,012) finalized on their own within about 10 minutes of the scores
    landing; no season finalize was needed. 0 null winners among them. Left
    unfinalized: only canceled, postponed, scheduled or no-status contests.
  - Side effect: 3 older 2016 contests now hold a winner that contradicts
    their (correct) score. The refresh rewrote their scores, and
    `CompetitorScoreUpdatedConsumerHandler` writes scores without re-deriving
    the winner. Tennessee State at Austin Peay (41-40 away, stored home),
    Tennessee at Texas A&M (38-45, stored null), UTSA at New Mexico (20-23,
    stored away). Fix is per-contest re-enrich. Any season refresh that
    changes a finalized contest's score can do this.
  - Done 2026-11-09: the 3 re-enriched (0 contradicting after). Franchise
    season enrichment enqueued for every season 2001-2025 (18:20-18:31 UTC).
    Check after drain: every team with a finalized game re-enriched; 0 W/L,
    wins or conference W/L mismatches against finalized contests in any
    season. Contest check 2001-2025: 0 score mismatches, 0 null winners,
    1 contradicting winner (Bridgewater State 2025, above), 236 flagged.
    Champion spot-check matches real overall records (USC 2004 13-0, Auburn
    2010 14-0, Ohio State 2014 14-1, Alabama 2015 14-1, Clemson 2016 14-1 and
    2018 15-0, LSU 2019 15-0, Alabama 2020 13-0, Michigan 2023 15-0).
    Conference W/L stays division-level (Alabama 2015 5-1, real SEC 7-1);
    resolved 2026-11-10 by PR #860 (Remaining, item 3).
  - This also clears the "same signature in other seasons" table below:
    2014, 2015, 2016, 2018 and 2019 now have 0 null winners.
- 236 re-enriched contests keep `AuditFlaggedUtc`; re-enrich clears only
  `AuditedUtc`, so the audit keeps skipping them. Data is correct.

**Conference W/L counts division games.** `EnrichFranchiseSeasonHandler`
counts a game as conference when the opponent shares `GroupSeasonId`, which is
the leaf group, so in conferences with divisions it is the division. Auburn
2010 shows 5-0 (SEC West) against a real 8-0 SEC. Long-standing behavior, not a
regression; matters for any War Room view of conference records. Fixed by PR
#860 and re-enriched 2026-11-10; Auburn 2010 now 8-0 (Remaining, item 3).

**Same signature in other seasons (pre-existing, not from this run).**
Finalized contests whose winner contradicts the score, all with a null winner:

| Season | Finalized | Null winner, unequal score |
|---|---|---|
| 2014 | 3,783 | 2,181 |
| 2015 | 3,728 | 2,177 |
| 2016 | 3,010 | 113 |
| 2018 | 2,069 | 507 |
| 2019 | 3,777 | 265 |

Every other season 2001-2025 has 0. All of these are pending audit. A null
winner counts as neither a win nor a loss in records, so these seasons' W/L
is understated. Remedy undecided; per-contest calls do not scale to 5,243
contests, so this needs either a season-level re-enrich that clears the
derived fields or a faster audit drain.

### Open findings

- **A conference member that is in no division list is never reached.**
  FAMU and Bethune-Cookman 2020 are still `NCAAF|NCAA|fcs|swac|swace`. ESPN's
  2020 MEAC (group 24) list has 9 teams including both (50, 2065), but the
  MEAC has two division children, North (169: 47, 2169, 2415, 2450) and South
  (170: 2428, 2448, 2569), which together hold only 7. Same in `types/2` and
  `types/3`. The leaf-only rule ([Design 2](#2-groupseasondocumentprocessor-follows-its-teams-index))
  assumed a conference's members are the union of its divisions; ESPN breaks
  that here. Walking every level's list is not a safe fix as is: Provider's
  republish cooldown is keyed on the document alone, so the second arrival of
  a team-season in the same run is dropped, and whether a team ends up at its
  division or its conference would depend on arrival order. Only these two
  rows in 2001-2025 are affected. Remedy undecided.
- **Teams in no ESPN FBS/FCS member list keep the TeamSeason link.** South
  Alabama 2009 and 2010 is still `NCAAF|NCAA|fbs|belt|beltw`. No
  member list reaches it, so the backfill cannot correct it. The map is
  doubly wrong: South Alabama joined FBS in 2012, and the Sun Belt had no West
  division in 2009. It has contests, so it counts in `|fbs` filters for those
  seasons. Remedy undecided; collect the full set after 2010-2025 first.
- **Membership corrections do not stamp `FranchiseSeason.ModifiedUtc`.**
  `TeamSeasonDocumentProcessor` never sets `ModifiedUtc` on any update (not
  only membership); the only writer is `EnrichFranchiseSeasonHandler`, and
  coverage audit section 5 reads `ModifiedUtc IS NULL` as "never enriched".
  Seq logs (old and new group ids per team-season) are the record of what the
  backfill changed. Fixed afterwards; see
  [`ModifiedUtc` fix](#modifiedutc-fix-2026-11-07-pr-858-merged).

### Remaining

1. Retire the `groupSeasonHierarchy.sql` patches.
2. Decide the remedy for the 4 residual team-seasons (South Alabama
   2009-2010, FAMU and Bethune-Cookman 2020).
3. Conference W/L: decided 2026-11-09 that conference records span
   divisions (division W/L is a specialty question). Merged as PR #860
   (`fix/conference-record-spans-divisions`): ESPN `conferenceCompetition`
   flag where a season has it (>= 10% of games flagged; NCAAFB 2014+),
   otherwise group roll-up (division to parent conference) excluding
   postseason and same-conference games with a "championship" note. Prod
   preview: NCAAFB 3,717 of 15,659 team-seasons change (2001-2026); NFL all
   825 change (division W/L to AFC/NFC). Spot-checks match real records
   (Auburn 2010 8-0, Florida 2008 7-1, Alabama 2015 7-1, Clemson 2016 7-1,
   Ravens 2023 8-4, Chiefs 2024 10-2, Lions 2024 11-1). Known gaps: 2001-2002
   title games still count (no notes); Alabama 2011 6-1 vs real 7-1 (missing
   game, 2011 coverage). Review follow-ups in the PR: a contest with more
   than one competition no longer throws, and duplicates merge (any flag,
   any championship note) so the result is deterministic.
   - Done 2026-11-10: deployed, then records re-enriched for NCAAFB
     2001-2026 and NFL 2001-2026 (all HTTP 202; the endpoint rejects years
     <= 2000, so NFL 1990-2000 was not run). Read-only check after drain:
     0 of 15,659 NCAAFB (2001-2025) and 0 of 825 NFL team-seasons differ
     from the preview logic; NCAAFB 2026 (271 differing before its own
     run) also 0. Stored records match the spot-checks: Texas 2005 8-0,
     Florida 2008 7-1, Auburn 2010 8-0, Ohio State 2014 8-0, Alabama 2015
     7-1, Clemson 2016 7-1, LSU 2019 8-0, Michigan 2023 9-0, Ravens 2023
     8-4, Chiefs 2024 10-2, Lions 2024 11-1; Alabama 2011 6-1 (known gap).
4. `CompetitorScoreUpdatedConsumerHandler` rewrites a finalized contest's
   score without re-deriving the winner. Fix staged 2026-11-10 on
   `fix/score-change-reenriches-finalized-contest`: a score change on a
   finalized contest enqueues the contest re-enrich (clears derived fields,
   re-derives from competitor scores, republishes `ContestFinalized` so both
   records recompute; pick scoring skips already-scored picks). Title games
   confirmed out of conference W/L (matches Wikipedia records).
5. Bridgewater State 2025 home/away winner; 236 `AuditFlaggedUtc` contests
   with correct data.

Done 2026-11-09: 2016/2018 score re-source (auto-finalized, no season
finalize needed) and season records re-enrich for 2001-2025, verified.

## Season 2000

Decided 2026-11-06: ESPN publishes no member lists for 2000, so 2000 keeps
the TeamSeason link. 2000 conference data is unverified; exclude 2000 from
conference-based analytics until a source exists.

## Appendix: affected team-seasons

`Prod` is the division and conference in `GroupSeasonMap`; `ESPN` is the
conference whose member list contains the team that season.

| Team | Seasons | Prod | ESPN | Count |
|---|---|---|---|---|
| arizona-state-sun-devils | 2001-2010 | fbs, big12 | fbs, pac12 | 10 |
| arizona-wildcats | 2001-2010 | fbs, big12 | fbs, pac12 | 10 |
| army-black-knights | 2001-2004 | fbs, American | fbs, usa | 4 |
| byu-cougars | 2001-2010 | fbs, big12 | fbs, mwest | 10 |
| california-golden-bears | 2001-2010 | fbs, acc | fbs, pac12 | 10 |
| cincinnati-bearcats | 2001-2004 | fbs, big12 | fbs, usa | 4 |
| colorado-state-rams | 2001-2012 | fbs, pac12 | fbs, mwest | 12 |
| delaware-blue-hens | 2001-2004 | fbs, usa | fcs, atl10 | 4 |
| east-carolina-pirates | 2001-2004 | fbs, American | fbs, usa | 4 |
| hofstra-pride | 2001-2004 | (none) | fcs, atl10 | 4 |
| houston-cougars | 2001-2004 | fbs, big12 | fbs, usa | 4 |
| james-madison-dukes | 2001-2004 | fbs, belt | fcs, atl10 | 4 |
| louisville-cardinals | 2001-2004 | fbs, acc | fbs, usa | 4 |
| maine-black-bears | 2001-2004 | fcs, caa | fcs, atl10 | 4 |
| maryland-terrapins | 2001-2004 | fbs, big10 | fbs, acc | 4 |
| massachusetts-minutemen | 2001-2004 | fbs, midam | fcs, atl10 | 4 |
| memphis-tigers | 2001-2004 | fbs, American | fbs, usa | 4 |
| new-hampshire-wildcats | 2001-2004 | fcs, caa | fcs, atl10 | 4 |
| northeastern-huskies | 2001-2004 | (none) | fcs, atl10 | 4 |
| oregon-ducks | 2001-2010 | fbs, big10 | fbs, pac12 | 10 |
| rhode-island-rams | 2001-2004 | fcs, caa | fcs, atl10 | 4 |
| richmond-spiders | 2001-2004 | fcs, pat | fcs, atl10 | 4 |
| san-diego-state-aztecs | 2001-2012 | fbs, pac12 | fbs, mwest | 12 |
| southern-miss-golden-eagles | 2001-2004 | fbs, belt | fbs, usa | 4 |
| stanford-cardinal | 2001-2010 | fbs, acc | fbs, pac12 | 10 |
| tcu-horned-frogs | 2001-2004 | fbs, big12 | fbs, usa | 4 |
| tulane-green-wave | 2001-2004 | fbs, American | fbs, usa | 4 |
| uab-blazers | 2001-2004 | fbs, American | fbs, usa | 4 |
| ucla-bruins | 2001-2010 | fbs, big10 | fbs, pac12 | 10 |
| usc-trojans | 2001-2010 | fbs, big10 | fbs, pac12 | 10 |
| utah-utes | 2001-2010 | fbs, big12 | fbs, mwest | 10 |
| villanova-wildcats | 2001-2004 | fcs, pat | fcs, atl10 | 4 |
| washington-huskies | 2001-2010 | fbs, big10 | fbs, pac12 | 10 |
| william-mary-tribe | 2001-2004 | fcs, pat | fcs, atl10 | 4 |
| south-florida-bulls | 2002-2004 | fbs, American | fbs, usa | 3 |
| tcu-horned-frogs | 2005-2011 | fbs, big12 | fbs, mwest | 7 |
| south-alabama-jaguars | 2009-2010 | fbs, belt | (not listed) | 2 |
| boise-state-broncos | 2011-2012 | fbs, pac12 | fbs, mwest | 2 |
| fresno-state-bulldogs | 2012 | fbs, pac12 | fbs, mwest | 1 |
| bethune-cookman-wildcats | 2020 | fcs, swac | fcs, meac | 1 |
| florida-am-rattlers | 2020 | fcs, swac | fcs, meac | 1 |
