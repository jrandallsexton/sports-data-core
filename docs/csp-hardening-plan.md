# CSP hardening plan

Follow-up tracking for two Content-Security-Policy weaknesses
surfaced by CodeRabbit on PR #311. Both were skipped at the time as
out-of-scope ("heavy lift") because PR #311 was an at-bat header
feature with a single-line CSP `wss://` hotfix; full CSP hardening
is its own coordinated work. This doc captures the plans so future
sessions don't have to rediscover the context.

CSP source: `src/UI/sd-ui/security-headers.conf`. Served by nginx
in front of the Vite-built React SPA. Same headers apply in every
environment because nginx is the only HTTP-layer config we own.

---

## Finding 1 — Parameterize the Firebase auth domain

### Problem

The CSP `frame-src` directive hardcodes the dev Firebase auth
domain:

```text
frame-src 'self' https://www.youtube.com https://accounts.google.com
          https://sportdeets-dev.firebaseapp.com;
```

Production currently trusts the **dev** Firebase tenant for iframe
embedding. Firebase auth uses an iframe-based redirect popup, so
this is load-bearing — without `sportdeets-dev.firebaseapp.com` in
`frame-src`, login breaks. But it also means a prod page can embed
a dev tenant frame, which is a (mild) trust boundary leak.

### Existing state

The file's header comment already documents this — has since the
config landed:

```nginx
# TODO: When migrating to a production Firebase project, parameterize the Firebase
# auth domain (sportdeets-dev.firebaseapp.com) across firebase.js, this CSP config,
# and Program.cs CORS origins. Use envsubst or nginx variable substitution at
# container startup so all layers share a single FIREBASE_AUTH_DOMAIN env var.
```

So the team has known about this for a while. The blocker is that
prod still uses the dev Firebase tenant — the parameterization only
becomes meaningful once a dedicated prod Firebase project exists.

### Touch points

Three files hardcode `sportdeets-dev.firebaseapp.com` (or related
literals):

1. `src/UI/sd-ui/security-headers.conf` — CSP `frame-src`.
2. `src/UI/sd-ui/src/firebase.js` — Firebase client config
   (`authDomain` field). This is build-time injected today.
3. `src/SportsData.Api/Program.cs` — CORS allowed origins.

A grep pass at implementation time should verify the list is
complete (other auth-flow consumers may exist).

### Plan

1. **Provision a prod Firebase project** (or confirm one exists).
   This is the actual prerequisite — without it the env var has
   nothing to point at and we'd just be shuffling the same literal.
2. **Define a single env var name** — `FIREBASE_AUTH_DOMAIN` is the
   obvious one. Used everywhere downstream.
3. **JS client**: Vite's build pipeline already supports
   `VITE_FIREBASE_AUTH_DOMAIN`. Read it in `firebase.js` instead of
   the hardcoded string. Set per-environment in the EAS build
   config and the local `.env.local`.
4. **Nginx CSP**: switch to `envsubst` at container entrypoint.
   Rename `security-headers.conf` → `security-headers.conf.template`,
   substitute `${FIREBASE_AUTH_DOMAIN}` at startup, write to the
   real conf path before nginx boots. Add the env var to the k8s
   deployment manifest in `sports-data-config`.
5. **API CORS**: `Program.cs` reads the same env var (via
   `IConfiguration`). Add to the Azure App Config layer for each
   environment.
6. **Verify both flows**: login redirect works in dev and prod, no
   CSP violation in the browser console after switching domains.

### Risks

- **Hard cutover**: if the env var is unset in any environment,
  Firebase login redirect frame breaks (CSP blocks). Belt and
  suspenders: keep a default value in the template that falls back
  to the dev domain so a missing env var doesn't take down login
  in lower environments.
- **Caching**: nginx CSP is per-response; no cache issue. Vite
  bundles the auth domain into JS at build time, so build-time
  injection is correct (no runtime fetch).

### Estimate

~half-day of focused work once a prod Firebase project exists. The
infra-config side (k8s deployment manifest, Azure App Config) is
the deploy-time risk; the code change is mechanical.

### CodeRabbit reference

PR #311 review thread on `src/UI/sd-ui/security-headers.conf` line 8.

---

## Finding 2 — Remove `'unsafe-inline'` from `script-src`

### Problem

Current `script-src` directive:

```text
script-src 'self' 'unsafe-inline' https://maps.googleapis.com
           https://apis.google.com https://analytics.sportdeets.com;
```

`'unsafe-inline'` defeats most of the CSP XSS protection for
scripts — any successful HTML injection can execute arbitrary JS.
Replacing it with nonces or hashes restores the protection.

### Current state

The SPA is built with **Create React App** (`react-scripts 5.0.1`),
served as static assets by nginx. Build output lives under
`src/UI/sd-ui/build/`.

Audit of the current production build (`build/index.html`) — **zero
inline `<script>` tags**. Just two externals:

```html
<script defer src="https://analytics.sportdeets.com/script.js" ...></script>
<script defer src="/static/js/main.5b1f5d35.js"></script>
```

The source template (`public/index.html`) also has no inline
scripts. CRA 5 bundles the webpack runtime into `main.*.js` rather
than emitting a separate inline runtime chunk, so the standard
`INLINE_RUNTIME_CHUNK=false` escape hatch isn't even needed here.

So **`'unsafe-inline'` is dead permission at the static-asset
layer**. The remaining question is runtime — whether third-party
scripts inject inline `<script>` children:

- **Google Maps** loads via `@react-google-maps/api`'s
  `useLoadScript` hook, which appends a `<script src=…>` tag with
  origin `https://maps.googleapis.com` (already allowlisted). Once
  loaded, the Maps JS API *may* inject inline scripts internally.
  Verify empirically.
- **Firebase auth** uses an iframe (covered by `frame-src`). The
  `firebase/*` SDK is bundled into `main.js`, no external load.
- **Analytics** is the external `script.js` referenced above.

### Approach

Because the build emits no inline scripts, no externalization work
is needed. The plan is a **staged drop** of `'unsafe-inline'`:

1. Ship CSP Report-Only alongside the enforcing header — when both
   are present, browsers enforce the existing header AND report
   violations against the stricter one. Lets us see what (if
   anything) trips without breaking prod.
2. Watch for violations across one or two releases — exercise
   login, map view, live game updates, every page that touches
   third-party JS.
3. If no violations, flip: drop `'unsafe-inline'` from the
   enforcing header and remove the Report-Only header.
4. If violations surface, decide per-case: add SHA-256 hash for a
   known-safe inline, or document the third-party as requiring a
   targeted exemption.

### Plan

1. **Add Report-Only header** to `security-headers.conf` mirroring
   the enforcing CSP minus `'unsafe-inline'` from `script-src`. The
   enforcing header stays unchanged in this step.
2. **Deploy and watch** — exercise the app in Chromium + Firefox
   (they enforce slightly differently). Check browser console for
   `Content-Security-Policy-Report-Only` violations. No `report-uri`
   endpoint exists today; console inspection is sufficient for a
   solo-dev product. (Adding a reporting endpoint is a separate
   follow-up if violations are too noisy to track manually.)
3. **Verify third-party flows**: Google Maps render, Firebase login
   popup, analytics script execution. Each should produce zero
   Report-Only violations.
4. **Cutover** — once the report-only header has been clean for a
   release, drop `'unsafe-inline'` from the enforcing `script-src`
   and remove the Report-Only header.

### Risks

- **Third-party scripts injecting inline children**. Google Maps is
  the prime suspect — once loaded, internal modules can inject
  inline `<script>` blocks. The origin allowlist covers external
  `src=…` loads but not inline children. The Report-Only watch
  period catches this before enforcing.
- **Cached `index.html` after cutover**. Not a real risk here —
  nginx serves the SPA shell and `index.html` isn't aggressively
  cached. CSP headers come from the response itself, no
  build-coupling.
- **Future inline introductions**. Once enforcing, any future code
  that does `dangerouslySetInnerHTML` with a `<script>`, or adds
  inline analytics snippets, will break silently. Document the
  enforcement in the file header so future contributors know to
  externalize.

### Estimate

Half a day for the report-only header + verification, then a
calendar gap (a release or two) before the cutover. The cutover
itself is a one-line edit.

### CodeRabbit reference

PR #311 review thread on `src/UI/sd-ui/security-headers.conf` line 8.

---

## Sequencing

Finding 1 and Finding 2 are independent. Suggested order:

1. **Finding 1 first** — small, isolated, low rollback risk. Lands
   alongside the prod Firebase project switch when that happens.
2. **Finding 2 second** — requires its own report-only watch
   period before enforcing. Larger calendar footprint but doesn't
   block #1.

Both should ship as their own PRs labeled `infra` / `security` so
the rationale is preserved in commit history.

## Reference

- `src/UI/sd-ui/security-headers.conf` — the CSP source.
- `src/UI/sd-ui/src/firebase.js` — Firebase client config.
- `src/SportsData.Api/Program.cs` — API CORS origins.
- PR #311 — landed the `wss://*.service.signalr.net` hotfix; CR
  comments on `security-headers.conf` line 8 surfaced these two
  findings.
