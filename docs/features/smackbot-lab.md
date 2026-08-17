# SmackBot Lab: preview, rate, and author the voice before it goes live

**Status**: Notification side (this change) · API composition and web page
follow · **Depends on**: docs/features/smackbot-voice.md (#643, #644)

## Why

SmackBot stays dark until the operator has laid eyes on real output. The Lab
is the admin surface for that: pick a league whose games are finished and
picks scored, see exactly what SmackBot *would have pushed* for every pick,
rate each line 0–4 stars, and author new phrases — all against real game
data.

The ratings are deliberately structured as **training data**: each row pairs
the pick facts (features) with the emitted line and a star label, so future
models can generate candidate taunts and be scored against operator taste.

## The preview replays the real pipeline

The preview endpoint lives in Notification and runs the send path's exact
code — same `PickSituationResolver`, same deterministic catalog selection,
same token formatting — minus the FCM dispatch. A rating therefore grades
precisely what a user would have received, not a simulation. Fidelity
extends to the gambling gate: previews derive `allowGamblingContent` from
the pick's ATS-ness exactly as the consumer does, rather than letting the
operator override it.

`ISmackPhraseCatalog` gains `ResolveDetailedAsync`, returning situation,
chosen `PhraseId`, rendered text, and a fell-back-to-standard flag; the send
path's `TryResolveAsync` becomes a thin wrapper over it, so the two can
never drift.

## Endpoints (Notification, `[ApiKeyAuth]`, `admin/smack`)

| Endpoint | Purpose |
|---|---|
| `POST preview` | batch of pick-fact payloads + voice → per pick: situation, phraseId, rendered text, fallback flag |
| `GET phrases` | full catalog, including inactive rows |
| `POST phrases` | create a phrase |
| `PUT phrases/{id}` | full update (text, situation, sport, weight, gambling flag, active, description) |
| `POST ratings` | upsert a rating for a previewed pick |

The API relays these server-to-server with the admin key from AppConfig; the
browser never holds Notification's key. Phrase text flows browser → API →
Notification → database at runtime and never enters the repo.

## Ratings schema

`SmackPreviewRating` — one row per (pick, voice), upserted so re-rating
after a phrase edit overwrites rather than duplicates:

| Column | Purpose |
|---|---|
| `PickId`, `ContestId`, `LeagueId`, `PickerUserId` | provenance back to the real pick |
| `Voice`, `Situation` | resolution context |
| `PhraseId` | the rated line; null = the preview fell back to standard copy |
| `RenderedText` | the exact string rated, immune to later phrase edits |
| `Stars` | 0–4, DB check constraint |
| `FactsJson` | the full fact payload — the training features |

Rating a fallback (no phrase available) is allowed and useful: a low star on
fallback rows marks situations whose bucket needs lines.

## Authoring loop

The fastest way to write good lines: add a phrase in the Lab, re-preview the
league, and read it rendered with real teams and scores. The operator content
rules from docs/features/smackbot-voice.md apply — notably that every
situation needs at least one `RequiresGamblingContent = false` line.

## Not in this change

API composition endpoints (league list, per-league scored-pick facts) and
the web page (`/app/admin/smack-lab`) follow in the next slices.
