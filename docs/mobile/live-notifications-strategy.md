# Live Notifications Strategy — payload is the moat, not the channel

**Status**: Strategy note, 2026-07-17. Forward-looking; not an implementation plan.
**Companion**: [`notifications-and-live-updates.md`](./notifications-and-live-updates.md) is the *how* (transport = SignalR + FCM, service home = `SportsData.Notification`, phased delivery). This doc is the *why* and the *design rule* that governs what those notifications should say.

---

## The trigger

On 2026-07-17, Google began pushing MLB **Live Activities** to the iOS lock screen — unprompted. Across a few minutes of screenshots: team records, a pregame win-probability bar, then full live game state (inning, count, outs, baserunner diamond, score), rendered natively on the lock screen with Google's distribution, for free.

It's genuinely impressive, and it's the moment to be clear-eyed about what it does and doesn't change.

## What it kills, and what it doesn't

**It commoditizes generic Gamecast.** Live scores, win probability, and game state on the lock screen are now table stakes, given away by the best-funded competitor on earth. **Do not invest a single sprint in beating anyone at generic live game tracking.** That fight is lost and pointless.

**It does not touch the moat.** Google's Live Activity shows *the game*. It structurally cannot show *"you took BOS on the run line, that pick is live in the top 1st, and you're up 4-2 on Dave in the Sunday league."* They have no personal pick'em data and it isn't their model. The thing that makes SportDeets worth opening during a live game is the **pick'em overlay**, not the game state.

The real asymmetry Google exposed is **distribution** (unprompted lock-screen reach vs. earning every install and opt-in), not the feature. That's the hard problem, and no notification cleverness solves it alone.

## The load-bearing design rule

> **The payload is the moat, not the delivery mechanism. Never send a generic score. Always send the league stake.**

Two notifications, same delivery budget, entirely different product:

| ❌ Imitating Google (badly) | ✅ The differentiator |
|---|---|
| "Rays 0, Red Sox 0 — top 1st" | "Your BOS pick just took the lead — you're up 4-2 in your matchup" |
| "Final: Red Sox 3, Rays 2" | "You won your Week 3 matchup 6-4. BOS pick cashed." |
| "Game starting soon" | "3 of your picks lock in 15 min — 2 still unset" |

If a notification could have come from ESPN or Google unchanged, it's the wrong notification. Every push should answer *"how are MY picks / MY league doing?"* — the "am I winning?" question that is the entire live-UI thesis.

This rule is channel-independent. It applies to in-app banners, FCM pushes, and any future Live Activity identically.

## Channel: push now, Live Activity later

**Push notifications are the correct launch architecture — not a compromise.**

- **Cross-platform with one payload.** Live Activities are **iOS-only** (ActivityKit). Android has no equivalent primitive — it has separate ongoing/"Live Update" notifications (Android 16). A push reaches mixed devices with one sender path; a Live Activity would be iOS-only work that still leaves Android uncovered.
- **Right for discrete moments.** A push is an interruption — ideal for events: *your pick took the lead*, *your matchup is decided*, *picks lock soon*. Launch needs exactly this.

**Live Activity is deferred until after the app-store push.** Two reasons, and neither is "we can't":

1. **Sequencing.** Mobile-ready-for-MLB-beta and the app-store submission come first.
2. **It's a native lift, not a JS feature.** A Live Activity is a Swift **widget extension** (SwiftUI layout compiled into the app) wired through an Expo **config plugin**, plus an ActivityKit push path. Expo *can* do this — the app already ships native code (Firebase, EAS dev clients, native OAuth) — but it is deliberate Swift work, not a screen. Do not file it under "Expo can't"; file it under "later, on purpose."

### Nuance worth preserving

A Live Activity is **not** overkill *for the pick'em use case* the way it is for Google's generic-score use. "Am I winning right now?" is a **continuous-state** question, and ambient glanceable state is precisely what Live Activities are good at and what a push is bad at (a push is a momentary interruption, not a persistent status). So the pick'em payload is a *better* fit for a Live Activity than the generic score Google is showcasing. It is "not v1," not "not for us."

When it is built, the defensible surface is a **pick'em Live Activity**: *"Your matchup: winning 4-2 · BOS pick live, top 1st"* — same lock-screen real estate and same moment Google occupies, carrying the one payload they can't render.

## How Live Activities work (reference, for when we build it)

So the eventual effort is scoped realistically:

- **The rich layout is local code, not payload.** The lock-screen card is a SwiftUI view (`ActivityConfiguration`) compiled into the app. Updates push only a small JSON `content-state` (a few hundred bytes matching a Swift `ContentState` struct); the device re-renders the pre-installed template. "How do you push a graphic that size" → you don't; you push ~300 bytes of state.
- **Update transport**: APNs push with `apns-push-type: liveactivity`, targeting the activity's push token.
- **Unprompted start**: **push-to-start** (iOS 17.2+) — a push can *start* an activity on a device not already running one. This is how Google appeared on the lock screen unbidden; the iOS "continue to allow Live Activities?" prompt gates it.
- **Scale**: **APNs broadcast channels** (iOS 18) — one update to a per-game channel fans out to all subscribers instead of per-token pushes. Makes per-pitch cadence economically sane.
- The hard infrastructure (render-from-tiny-state, broadcast fan-out) is Apple's, free to any developer. What we'd build is the SwiftUI card and the pick'em-aware state feed — both things within reach.

## Decision summary

- **Now (launch)**: FCM push, cross-platform, event-driven. Payload = league stake, never generic score.
- **Deferred (post-store, iOS-only)**: pick'em Live Activity for continuous "am I winning?" state.
- **Never**: compete on generic Gamecast / generic live scores. Commodity, given away free.
