# Team mark design brief (for Claude Design)

This document is a self-contained brief. Paste it into Claude Design
or any design-ideation surface and it should be enough context to
produce useful directions.

## Context

sportDeets is a multi-sport pick'em platform — users pick winners,
spreads, and over/unders across NFL, NCAA football, MLB, with NBA /
NHL / PGA on the roadmap. The product surfaces team identity heavily
throughout the UX: matchup cards, standings, live-game tiles, pick
selection flows, league rosters, profile favorites, and push
notification thumbnails.

Up to now, the data backend has sourced and stored real team logos
(NFL, NCAA, MLB, etc.) from ESPN's public asset URLs and rehosted
them in our own Azure Blob Storage. **We cannot ship this to either
app store.** The plan needs to change before Apple App Store / Google
Play submission.

## The constraint

Team logos are trademarked. Sourcing them from ESPN and rehosting
them doesn't grant us rights — ESPN has licensing deals; we don't.
Licensing the marks ourselves is not viable:

- **NFL Properties:** ~$100k/yr minimums + revenue share. They
  generally don't license to small operators at all.
- **NCAA / CLC (Collegiate Licensing Company):** Power 5 schools
  bundled, also in the $100k+ range for the bundle.
- **MLB Advanced Media, NBA Digital, NHL Enterprises:** more
  approachable historically but still well outside a pre-revenue
  solo-founder budget.

App store review will flag unlicensed marks. A "show real logos to
my friends, generic to everyone else" workaround was considered and
rejected — it doesn't solve the underlying infringement (server is
still hosting unlicensed assets), and Apple specifically polices
review-evasion patterns where the app behaves differently for
review accounts vs. real users.

**Conclusion:** sportDeets needs its own visual marks for teams,
generated programmatically from data we already have. That's a
permanent change, not a launch hack.

## What we already have

For every team (we use "Franchise" / "FranchiseSeason" in our
schema), we store:

- **Primary brand color** (hex / RGB) — sourced from ESPN, which got
  it from each league's official brand guide. Colors are not
  trademarked; we can use these forever.
- **Secondary brand color** (hex / RGB).
- **Team abbreviation** — 2-4 characters (DAL, NYY, BOS, MTL, ALA,
  USC).
- **Team name** — location + nickname ("Dallas Cowboys", "New York
  Yankees").
- **Sport / League** — which sport the team plays.
- **Year** for `FranchiseSeason` — colors and abbreviations can
  change across seasons (rare but happens, especially in NCAA).

We do NOT have (and don't want to invent):

- Mascot illustrations or silhouettes.
- Stylized team-specific glyphs.
- Anything that derives from the real mark, even loosely.

## Where the mark appears

The generated mark needs to work at multiple sizes and in multiple
contexts:

| Context | Size | Notes |
|---|---|---|
| Push notification thumbnail | 24-32px | Must be recognizable at tiny size, low color depth |
| Standings table row | 24-32px | Often alongside ~20 similar marks; differentiation matters |
| Matchup card (mobile + web) | 48-72px | The hero placement; this is where most users see the mark |
| Pick selection flow | 72-96px | Side-by-side comparison: two teams; contrast between them matters |
| Live game tile (Command Center) | 96-128px | May animate during live updates |
| Team detail / Franchise page header | 128-256px | Larger, more decorative |
| Profile favorite team | 96-128px | Single team focus |

Light theme and dark theme are both required. Some teams have
near-black primaries (Raiders), some near-white (Yankees away
palette), some pastel — the mark needs to look intentional in both
themes regardless of palette.

## Multi-sport reality

A helmet shape works beautifully for NFL but feels wrong for MLB or
NBA. Options:

- **One universal shape** that's sport-agnostic (a roundel, a
  shield, a hexagon, a custom geometric mark) — works everywhere,
  no per-sport variant logic.
- **Sport-specific shape families** (helmet for football, cap for
  baseball, jersey block for basketball/hockey) — more visually
  appropriate but more design + engineering work.
- **Hybrid** — universal shape with subtle sport hint (e.g., a
  small icon in a corner of the roundel).

We don't have a strong opinion yet; this is exactly what we want
the design exploration to weigh in on.

## Requirements

1. **Parameterized from data.** Given (primary color, secondary
   color, abbreviation, sport), the mark generator should produce
   a deterministic SVG. No per-team manual design work.
2. **Distinctive at small sizes.** A list of 20 marks should be
   visually scannable.
3. **Reads as intentional design**, not "we couldn't afford the
   real ones." Yahoo Fantasy ran generic marks for years; the
   visual identity became theirs, not a compromise.
4. **Accessible contrast.** Some teams have similar colors
   (Cardinals red + Chiefs red + 49ers red). The mark should
   include an internal contrast element so it doesn't reduce to a
   blob of one color.
5. **Legally clean.** No element traceable to any real team mark.
   No mascot silhouettes. No protected symbology.
6. **SVG-first.** Generated at data-sourcing time, cached to blob
   storage, served from existing URL pattern (drop-in replacement
   for current logo URLs).

## Non-goals

- We are NOT trying to replicate ESPN's logo treatment.
- We are NOT designing per-team marks; we're designing a parametric
  system that produces a mark per team.
- We are NOT trying to look "official" — looking distinctly
  sportDeets-y is the goal.

## Real team data for sketching

Below are real teams across sports with their actual color data
(approximations from ESPN's brand records — real values would be
pulled from the DB at generation time). Useful for stress-testing a
design direction against tough cases.

### NFL

| Team | Abbr | Primary | Secondary | Notes |
|---|---|---|---|---|
| Dallas Cowboys | DAL | #003594 (navy) | #B0B7BC (silver) | Iconic, common |
| Pittsburgh Steelers | PIT | #000000 (black) | #FFB612 (gold) | Black primary case |
| Cleveland Browns | CLE | #311D00 (brown) | #FF3C00 (orange) | Brown is rare and tricky |
| Las Vegas Raiders | LV | #000000 (black) | #A5ACAF (silver) | Black + silver — both low-saturation |
| Miami Dolphins | MIA | #008E97 (teal) | #FC4C02 (orange) | High-contrast vibrant pair |

### MLB

| Team | Abbr | Primary | Secondary | Notes |
|---|---|---|---|---|
| New York Yankees | NYY | #003087 (navy) | #FFFFFF (white) | Near-white secondary |
| Los Angeles Dodgers | LAD | #005A9C (blue) | #FFFFFF (white) | Similar to NYY palette |
| San Diego Padres | SD | #2F241D (brown) | #FFC425 (gold) | Brown again, tough |
| Boston Red Sox | BOS | #BD3039 (red) | #0C2340 (navy) | Classic two-strong palette |

### NBA

| Team | Abbr | Primary | Secondary | Notes |
|---|---|---|---|---|
| Boston Celtics | BOS | #007A33 (green) | #BA9653 (gold) | Green + gold uncommon |
| LA Lakers | LAL | #552583 (purple) | #FDB927 (gold) | Vibrant pair |
| Brooklyn Nets | BKN | #000000 (black) | #FFFFFF (white) | Pure mono — tough to differentiate |

### NCAA Football

| Team | Abbr | Primary | Secondary | Notes |
|---|---|---|---|---|
| Alabama Crimson Tide | ALA | #9E1B32 (crimson) | #FFFFFF (white) | Crimson family is large in NCAA |
| Michigan Wolverines | MICH | #00274C (navy) | #FFCB05 (maize) | Navy + yellow, common |
| Oregon Ducks | ORE | #154733 (green) | #FEE123 (yellow) | Dark green primary |

### Tough cases to design against

- Multiple teams with similar palettes (the 4-5 NFL red teams, the
  2-3 MLB navy-and-white teams).
- Teams with monochrome palettes (Brooklyn Nets — black + white).
- Teams whose primary is near-black or near-white (need internal
  contrast).
- Teams whose colors are visually similar but they're rivals (e.g.,
  Michigan vs. Ohio State both have a yellow component).

## What we'd like back from the design exploration

3-5 distinct visual directions, each with:

1. **The concept** — one paragraph describing the visual idea (e.g.,
   "Heraldic shield with a horizontal bar of secondary color and the
   team abbreviation in primary").
2. **5-8 example renders** using the real color data above, ideally
   covering multiple sports and at least one tough case (monochrome,
   similar-palette pair).
3. **Pros and cons** as you see them.
4. **Multi-sport handling** — does the direction work universally or
   does it need sport-specific variants?
5. **SVG sketch / wireframe** that's clear enough we can implement
   from it.

After we pick a direction (or a hybrid), we'll define the SVG
template, wire it into the ingest pipeline, regenerate marks for
every team in the DB, and swap them into the existing logo URLs.
The mark system also becomes a sportDeets brand asset, not a
workaround — controllable, consistent, animatable, accessible.

## References (mention as inspiration, not copy)

- **Yahoo Fantasy** historical generic marks (before they got
  licensing deals) — heraldic shields with team abbreviations.
- **SeatGeek** team marks — abstract roundels with letter monograms.
- **Sleeper** team marks — minimalist mono-color cards.
- Heraldic / vexillological traditions (flags, crests).
- Older pro-sports branding eras (1950s-70s) where letter
  monograms dominated over mascot art.

None of these should be copied. Each is useful as a sketch of
"things that have worked for unlicensed sports identity."
