# sportDeets Icon Regeneration — claude.design Handover

## What already happened (no action needed)

The master SVG artwork was drifting **left** in the 1024 canvas. It's now fixed directly in both source SVGs (kept byte-identical):

- `src/UI/sd-mobile/assets/images/sportdeets-icon-master.svg`
- `src/UI/sd-ui/src/assets/sportdeets-icon-master.svg`

Two shifts, **verified by rendering** the SVG (not just math): the cyan **D** moved +9px right so it's centered in the canvas (equal 297px left/right margins), and the knocked-out **S** moved to x=487 — the D's **optical center** (its area centroid). Critically, that optical center sits ~25px **left** of the geometric bounding-box center (512), because the solid left spine outweighs the tapering right bulge. Centering the S on the geometric center instead makes it look visibly *too far right* — a trap worth flagging to whoever touches this next. Vertical was already centered.

**The web icon is already fixed** — `Wordmark.jsx` renders this SVG live, so no export is needed for web display. claude.design's job is to regenerate the **raster** files (mobile app icons + favicons) from the corrected SVG.

## Brand spec (reference)

- **Mark:** italic bold "S" knocked out of a cyan capital "D" on a near-black rounded square.
- **Colors:** cyan `#61dafb`, near-black `#0d1117`.
- **Canvas:** 1024×1024; rounded-square corner radius 228 (≈22%).

---

## 1) What to upload to claude.design

**Required**
- `sportdeets-icon-master.svg` — the corrected file (either copy; they're identical). This is the exact, final artwork.

**Optional (visual reference only)**
- current `src/UI/sd-mobile/assets/images/icon.png` (1024)
- current `src/UI/sd-mobile/assets/images/android-icon-foreground.png` (1024)

---

## 2) Prompt for claude.design (copy-paste)

> I'm regenerating the app-icon set for "sportDeets" from a single master SVG (attached). **Do not redraw or re-center the artwork** — it's final and correctly centered. Use it exactly as the source and export the platform-correct raster set below. Note: the "S" is intentionally positioned slightly left of the icon's geometric center for optical balance inside the "D" (the D's solid spine shifts its visual center left) — **preserve its exact placement; do not "straighten" or recenter it.** All PNGs sRGB, no embedded color profiles.
>
> Brand mark: an italic bold "S" knocked out of a cyan (`#61dafb`) capital "D" on a near-black (`#0d1117`) rounded square, corner radius ≈22% of the icon size.
>
> **iOS / App Store — full square, fully opaque, `#0d1117` fills the entire square including the corners. Do NOT bake rounded corners and do NOT use transparency (iOS applies its own mask):**
> - `icon.png` — 1024×1024
> - `icon-1024-appstore.png` — 1024×1024 (identical)
>
> **Android adaptive icon — the glyph ONLY, on a fully transparent background, with the artwork sized inside the center 66% "safe zone" (the outer ~17% on every edge may be cropped by the launcher):**
> - `android-icon-foreground.png` — 1024×1024
> - `android-icon-background.png` — 1024×1024, solid `#0d1117` (must match the foreground size — Expo's `adaptiveIcon.backgroundImage` requires equal dimensions)
> - `android-icon-monochrome.png` — 432×432, a single flat white (`#ffffff`) silhouette of the same glyph on transparent, same safe-zone sizing (for Android 13+ themed icons)
>
> **Splash — the glyph centered on `#0d1117` with generous padding:**
> - `splash-icon.png` — 1024×1024
>
> **Favicons — the rounded-square brand icon (rounded corners OK, transparent outside the square OK):**
> - `favicon.png` — 48×48
> - `favicon-16.png` — 16×16
> - `favicon-32.png` — 32×32
> - `favicon-180.png` — 180×180
> - `favicon-196.png` — 196×196
> - `favicon.ico` — multi-size, containing 16×16 and 32×32
>
> Keep the glyph proportions and position identical across every size so the mark reads consistently. Return each as an individually downloadable file with exactly these names.

---

## 3) Where each returned file goes

**Mobile → `src/UI/sd-mobile/assets/images/`**

| File | Size | Notes |
|---|---|---|
| `icon.png` | 1024² | iOS + primary app icon |
| `icon-1024-appstore.png` | 1024² | App Store Connect upload |
| `splash-icon.png` | 1024² | Splash |
| `android-icon-foreground.png` | 1024² | Adaptive foreground |
| `android-icon-background.png` | 1024² | Adaptive background — must equal the foreground size to be usable as `backgroundImage` (see wiring note) |
| `android-icon-monochrome.png` | 432² | Themed icon (see wiring note) |
| `favicon.png` | 48² | Expo web favicon |

**Web → `src/UI/sd-ui/public/`**

| File | Size |
|---|---|
| `favicon.ico` | 16/32 |
| `favicon-16.png` | 16² |
| `favicon-32.png` | 32² |
| `favicon-180.png` | 180² |
| `favicon-196.png` | 196² |

---

## 4) After the files come back

1. Drop the mobile rasters into `src/UI/sd-mobile/assets/images/` and the web icons into `src/UI/sd-ui/public/`, overwriting the placeholders (same filenames → no config edits needed).
2. **Wiring note:** `app.json`'s `android.adaptiveIcon` now wires `foregroundImage` (`android-icon-foreground.png`) + `monochromeImage` (`android-icon-monochrome.png`, the Android 13+ themed icon) over a flat `#0d1117` `backgroundColor`. It does **not** use `backgroundImage`. The committed `android-icon-background.png` is currently **512×512 and unused** — harmless as-is, but if you ever switch to an image background it must be regenerated at **1024×1024** to match the foreground (Expo rejects a mismatched `backgroundImage`); do not wire the 512 file.
3. **Likely-dead asset:** `src/UI/sd-mobile/assets/images/favicon/` is a second, generated favicon set that `app.json` doesn't reference — verify before spending effort there. `src/UI/sd-ui/public/helmet.svg` is a separate mark, not the app icon.
4. **Rebuild / ship:** web is a standard CRA build; mobile needs an Expo/EAS build. Bump `ios.buildNumber` and `android.versionCode` in `app.json` if submitting to the stores.

---

*Root source of truth: the two `sportdeets-icon-master.svg` files. Every raster above is downstream of them — if the mark ever changes again, edit the SVG, keep both copies in sync, and re-run this handover.*
