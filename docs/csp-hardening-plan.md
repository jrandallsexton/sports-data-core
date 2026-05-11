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

The SPA is built with Vite, served as static assets by nginx. After
`vite build`, the output `index.html` contains:

- A `<script type="module" crossorigin src="/assets/index-XXXX.js">`
  tag — external, doesn't need nonce/hash.
- Vite-injected inline module preload + runtime bootstrap blocks —
  these are the actual reason `'unsafe-inline'` is currently in
  place.

Plus runtime concerns:

- **Google Maps** (`https://maps.googleapis.com`) injects inline
  scripts when loaded via the JS API. Already allowed via origin,
  but worth verifying the injected scripts don't trip CSP.
- **Firebase auth** opens its iframe with its own script tags
  (frame-src handles those; not in scope here).
- **Analytics** loads as an external script tag; no inline.

### Two approaches

**Approach A: externalize all inline scripts.** Configure Vite to
emit zero inline scripts (`build.modulePreload.polyfill: false`,
review the output for any remaining inline blocks). Drop
`'unsafe-inline'`, add no replacement — `'self'` covers the
externalized bundle.

- Pros: simple CSP, no runtime templating, nginx stays a pure
  static-file server.
- Cons: needs a build audit; some Vite features (HMR runtime,
  module preload polyfill) emit inline by default. Some may
  require switching to hash-based SRI instead of nonces.

**Approach B: per-response nonce.** nginx generates a random nonce
per request, substitutes it into both the CSP header and every
inline `<script>` tag in the served `index.html` via
`ngx_http_sub_module`.

- Pros: handles arbitrary inline scripts without rebuilding.
- Cons: nginx becomes a templating layer for HTML. `index.html`
  must serve with `Cache-Control: no-store` (a cached nonce is
  worthless and breaks subsequent loads). Adds attack surface
  (sub-module misconfig). CDN behavior needs reckoning.

**Recommendation: Approach A.** Cleaner long-term, fits the static-
serving model, no per-response state. The audit is the real cost
and pays back in simpler ops.

### Plan (Approach A)

1. **Audit**. Run `vite build` locally, grep
   `dist/index.html` for `<script>` tags without `src`. Capture
   each inline block — count, size, what it does.
2. **Externalize**. For each inline block, decide:
   - Module preload polyfill → disable via Vite config; modern
     browsers handle native module preload.
   - Vite legacy runtime (if present) → likely not needed for our
     browser support matrix.
   - Anything else → move to a separate file under `public/` or
     `src/` and reference via `<script src="...">`.
3. **Add SRI hashes** (`integrity` attribute) on the externalized
   scripts so we can drop `'unsafe-inline'` and add
   `'sha256-...'` hashes if any inline survives.
4. **Drop `'unsafe-inline'`** from `script-src` in
   `security-headers.conf`.
5. **Test in Firefox + Chromium** — they enforce slightly
   differently. Verify in CSP report-only mode first
   (`Content-Security-Policy-Report-Only` for a release or two,
   then flip to enforcing).
6. **Verify third parties** still work: Google Maps script
   injection, Firebase popup, analytics.

### Risks

- **Vite emits more inline than we think.** The audit might find
  something hard to externalize (HMR runtime in some dev configs,
  but that's dev-only). Production builds should be cleaner.
- **A stale CSP without `'unsafe-inline'` while production still
  serves inline scripts** bricks the app. Use `Content-Security-
  Policy-Report-Only` for at least one release to surface
  violations before enforcing.
- **Third-party scripts injecting inline children**. Google Maps
  is the prime suspect — when the JS API loads, it can inject
  `<script>` tags. The origin-allowlist (`https://maps.googleapis.com`)
  should cover the src=… variant; inline children would need
  `'unsafe-inline'` or a separate exemption. Verify empirically.

### Estimate

~1-2 days, mostly audit + verification. Build/config change is
small. The "release once with report-only, watch for violations,
then enforce" cadence stretches calendar time across two deploys.

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
