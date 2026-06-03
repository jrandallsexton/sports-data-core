# Firebase Project Migration — `sportdeets-dev` → `sportdeets`

**Status:** Proposed
**Date:** 2026-06-02
**Driver:** Retire the misnamed `sportdeets-dev` Firebase project (currently serving production traffic) and consolidate on the actual `sportdeets` project before launch. The most visible symptom of the current setup is the Google sign-in consent dialog displaying "sportdeets-dev" to end users — a brand-fidelity bug that has to be gone before public launch.

## Why one project, not two

The two-project Firebase pattern (dev + prod) is best-practice for teams that need to validate risky changes against test users before exposing real ones. sportDeets at MVP stage doesn't cash in on that benefit yet — `sportdeets-dev` has been treated as production from day one (real auth, real picks, real leagues). Maintaining two projects doubles the config surface (per-build `GoogleService-Info.plist`, two service accounts, two APNs uploads, two analytics dashboards) for value not currently extracted.

If a preview / staging environment becomes useful later (e.g., to test push notification campaigns without spamming real users, or to validate destructive schema changes against a separate auth pool), spin up `sportdeets-staging` then. Decision deferred until the pain is real.

## Scope

### What gets migrated

- **Firebase Authentication users** — UIDs preserved via the `auth:export` / `auth:import` CLI flow. Postgres `User` rows tied to Firebase UIDs keep working without modification.
- **API service account** — generated fresh from the new project; replaces values in Azure App Config under `CommonConfig:Firebase:*`.
- **Mobile client config** — `GoogleService-Info.plist` regenerated from the new project, replaces the file at `src/UI/sd-mobile/GoogleService-Info.plist`.
- **APNs Auth Key** — the existing `.p8` is account-wide on Apple's side and re-uploads cleanly to the new project's Cloud Messaging configuration. Same Team ID, same Key ID.

### What does NOT migrate (and that's fine)

- **FCM device tokens** — tokens are project-scoped. Every installed app gets a fresh token on next launch under the new project. Zero impact since there's no production push consumer yet.
- **Analytics / Crashlytics / Performance history** — fresh start in the new project. Acceptable given we don't depend on historical funnels for live decisions.
- **OAuth consent screen branding** — has to be re-configured in the new project's underlying GCP Console (this is where the "sportdeets-dev" string actually lives — see [§OAuth consent screen branding](#oauth-consent-screen-branding) below).

## Prerequisites

- [ ] Confirm `sportdeets` Firebase project exists. If not, create it (Firebase Console → Add project).
- [ ] Apple Developer Console access (for re-uploading the existing `.p8`).
- [ ] Azure App Config write access (for `CommonConfig:Firebase:*` updates across labels).
- [ ] Firebase CLI installed locally (`npm install -g firebase-tools`) and authenticated.
- [ ] Inventory of Firebase Auth providers currently enabled on `sportdeets-dev` (Google, email/password, etc.) — needs to match on the new project.
- [ ] List of Azure App Config labels currently carrying Firebase config (likely `Development.{Sport}.SportsData.Api` and `Production.{Sport}.SportsData.Api` per sport mode).

## Migration steps

### Phase 1 — Prep the target project (no live impact)

1. **Register the iOS app.** Firebase Console → `sportdeets` project → Add app → iOS. Bundle ID: `com.sportdeets.mobile`. App nickname: `sportDeets iOS`. Download the new `GoogleService-Info.plist` — keep it locally for Phase 5.
2. **Enable Auth providers** matching `sportdeets-dev`. Authentication → Sign-in method tab. At minimum: Google. Add any others currently enabled on dev.
3. **Configure Authorized domains** in the Auth provider section to match (`sportdeets.com`, `api.sportdeets.com`, etc.).
4. **Upload the APNs Auth Key.** Project Settings → Cloud Messaging → Apple app configuration → upload the existing `.p8` to BOTH the development and production slots. Key ID and Team ID are the same as currently used on `sportdeets-dev`.
5. **Generate a new service account JSON.** Project Settings → Service Accounts → Generate New Private Key → download. This is the source of truth for the API's `CommonConfig:Firebase:*` values in Phase 4.

### Phase 2 — Export users from `sportdeets-dev`

1. Run from a trusted local machine:
   ```bash
   firebase auth:export users.json --project sportdeets-dev
   ```
2. The CLI output prints the hash algorithm + key used by `sportdeets-dev`'s Auth — **copy these exactly**. You need them verbatim for Phase 3.
3. Verify the user count in `users.json` matches Firebase Console's user count for `sportdeets-dev`.
4. Stash `users.json` somewhere safe (Bitwarden / 1Password attachment). Contains PII.

### Phase 3 — Import users into `sportdeets`

1. Run:
   ```bash
   firebase auth:import users.json --project sportdeets \
     --hash-algo <FROM_PHASE_2> \
     --hash-key <FROM_PHASE_2> \
     --rounds <FROM_PHASE_2> \
     --mem-cost <FROM_PHASE_2>
   ```
   (Exact flag set depends on the algorithm Phase 2 reported. The most common case is `HMAC_SHA256` with just `--hash-key`.)
2. Verify Authentication → Users count in `sportdeets` matches `sportdeets-dev`.
3. Spot-check: pick one known user. UID in `sportdeets` should match UID in `sportdeets-dev`. If they differ, the import didn't preserve UIDs — abort and figure out why before continuing (Postgres ties UIDs to picks; mismatch breaks every existing user's account).

### Phase 4 — Cut the API over (Azure App Config)

For each label currently holding Firebase config (likely the full set: `Development.BaseballMlb.SportsData.Api`, `Development.FootballNcaa.SportsData.Api`, `Development.FootballNfl.SportsData.Api`, plus the `Production.*` variants):

Update the following keys from the new service account JSON's values:

| Key | New value source |
|---|---|
| `CommonConfig:Firebase:ProjectId` | `project_id` field |
| `CommonConfig:Firebase:PrivateKeyId` | `private_key_id` field |
| `CommonConfig:Firebase:PrivateKey` | `private_key` field (preserve newlines) |
| `CommonConfig:Firebase:ClientEmail` | `client_email` field |
| `CommonConfig:Firebase:ClientId` | `client_id` field |
| `CommonConfig:Firebase:ClientX509CertUrl` | `client_x509_cert_url` field |

These stay the same (standard Google service account fields):

- `CommonConfig:Firebase:Type`
- `CommonConfig:Firebase:AuthUri`
- `CommonConfig:Firebase:TokenUri`
- `CommonConfig:Firebase:AuthProviderX509CertUrl`
- `CommonConfig:Firebase:UniverseDomain`

After updating: restart the API in each environment that's affected. Verify Firebase Auth still works (sign in flow, `/user/me` returns the expected user data).

### Phase 5 — Cut the mobile app over

1. **Replace `src/UI/sd-mobile/GoogleService-Info.plist`** with the file downloaded in Phase 1. Commit.
2. **Verify `PROJECT_ID` in the new file** says `sportdeets`, not `sportdeets-dev`.
3. **Bump `ios.buildNumber`** in `app.json` (EAS rejects builds with duplicate build numbers).
4. **Open PR** for the .plist replacement + buildNumber bump. Merge after CI/CR.
5. **Build:** `eas build --profile production --platform ios`.
6. **Submit to TestFlight** when build completes.
7. **Install** on a test device.
8. **Validate:**
   - Sign-in dialog shows `sportDeets` branding (not `sportdeets-dev`) — see [§OAuth consent screen branding](#oauth-consent-screen-branding) below if it still shows the old name.
   - Existing user can sign in with their old credentials (UID preserved per Phase 3 spot-check).
   - `/user/me` returns the expected user object.

### Phase 6 — OAuth consent screen branding

The "sportdeets-dev" string in the Google sign-in dialog is the **OAuth Application Name**, configured in GCP Console (not Firebase Console). To fix:

1. Open the underlying GCP project for `sportdeets` — Firebase Console → Project Settings → Service Accounts tab → "Manage all service accounts" link opens GCP Console scoped to the right project.
2. Navigate to **APIs & Services → OAuth consent screen**.
3. Set **App name** to `sportDeets` (matching brand).
4. Set **User support email** to `jrandallsexton@gmail.com` (or a support alias if you have one).
5. Set **Developer contact information** email.
6. Add **Authorized domains** matching production (`sportdeets.com`).
7. (Optional but recommended) Upload an app logo for the consent screen.
8. Save.

Changes propagate to the consent screen within minutes. Verify by signing out and back in on a fresh browser session.

### Phase 7 — Decommission `sportdeets-dev`

**Do NOT rush this.** Keep `sportdeets-dev` operational as a rollback path for at least 30 days post-cutover.

After confidence window (~30 days, no migration-related incidents):

1. **Disable sign-in providers** in `sportdeets-dev` (Authentication → Sign-in method → toggle each off). Prevents anyone from creating new accounts there by accident.
2. **Leave the project in place** for another 30+ days as a cold-rollback option.
3. **Eventually delete** `sportdeets-dev` once you're certain no service still references it (Firebase Console → Project Settings → bottom of page → Delete project).

## Rollback plan

If the migration hits an unrecoverable problem (UIDs didn't import correctly, OAuth misconfigured, etc.):

1. **Revert Azure App Config** values for the `CommonConfig:Firebase:*` keys back to the `sportdeets-dev` values. Restart the API.
2. **Revert `src/UI/sd-mobile/GoogleService-Info.plist`** to the old file via git.
3. **Rebuild mobile** with the reverted config (`eas build --profile production --platform ios`).
4. **Resubmit to TestFlight**, install, verify the old setup works again.

UIDs in `sportdeets` are a superset (or equal set) of `sportdeets-dev`'s — the rollback doesn't lose anyone, just defers the migration.

## Validation checklist (post-cutover)

- [ ] Sign-in flow works end-to-end (Google OAuth completes).
- [ ] OAuth consent screen displays `sportDeets`, not `sportdeets-dev`.
- [ ] Existing user signs in successfully with their old credentials.
- [ ] `/user/me` returns the expected user data (UID matches the Postgres row).
- [ ] User can submit a pick (proves the full API → DB flow under the new auth project).
- [ ] User can view leagues / leaderboard (read-side flow works).
- [ ] New sign-up creates a user in `sportdeets` (not `sportdeets-dev`).
- [ ] Firebase Console → Authentication → Users in `sportdeets` is growing; `sportdeets-dev` is not.
- [ ] API logs (Seq) show no Firebase ID token verification failures.
- [ ] Mobile crash-free rate is unchanged from pre-migration baseline.

## Open questions

- **Migration window timing.** Is there a low-traffic window worth picking? sportDeets at this stage probably has near-zero concurrent users, so any time works — but worth confirming.
- **Communication.** Should existing users be told anything? Probably not — UID preservation + same auth providers mean it should be transparent to them.
- **Web app (`sd-ui`)?** Does the web client also read from a Firebase config? If so, that config also needs updating in Phase 5 alongside mobile. Verify before cutover.
- **Pre-existing rollout work.** Once this is complete and stable, the push notifications saga can resume against `sportdeets`. Existing FCM tokens on `sportdeets-dev` would be invalidated anyway by the cutover, so the push-notification setup we already paid for (APNs Auth Key uploads, capability enablement) needs to be redone in the new project (covered in Phase 1 step 4).
