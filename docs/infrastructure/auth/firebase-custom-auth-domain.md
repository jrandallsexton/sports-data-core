# Firebase Custom Auth Domain — `auth.sportdeets.com`

**Status:** Proposed
**Date:** 2026-06-03
**Driver:** Replace `sportdeets.firebaseapp.com` in the Google sign-in OAuth dialog with a branded `sportdeets.com` subdomain.

## Problem

After the `sportdeets-dev` → `sportdeets` Firebase project migration (see `docs/firebase-project-migration.md`), the Google sign-in consent dialog reads:

> Sign in to **sportdeets.firebaseapp.com**

The "sportdeets-dev" string is gone — Phase 6 of the migration (OAuth consent screen App Name) handled that. But Google's consent dialog still shows the **domain serving the auth flow** on a separate line, which is Firebase's default `sportdeets.firebaseapp.com`. To a knowledgeable reader, that flags the app as off-the-shelf infrastructure-as-product rather than a polished consumer product. To a generic user it's slightly off-brand.

The default `*.firebaseapp.com` auth domain is fine for early-stage testing but worth fixing before any public marketing push or App Store review — reviewers in particular sometimes flag inconsistent domain branding as a metadata mismatch.

## Solution

Configure a custom subdomain you control (`auth.sportdeets.com` is the conventional pick) as a Firebase Hosting custom domain, then update `firebase.js` to use it as the `authDomain`. Firebase serves the same auth handlers under the new host. Google's OAuth dialog then shows the branded domain instead of the Firebase default.

The auth subdomain pattern (rather than using bare `sportdeets.com`) keeps the auth flow off whatever surface you use for the main marketing site / web app, avoiding any DNS / SSL conflicts between Firebase Hosting and your existing hosting.

## Prerequisites

- [ ] DNS access for `sportdeets.com` (ability to add TXT, A, AAAA records).
- [ ] Firebase Console access for the `sportdeets` project.
- [ ] Web deploy access (the change requires a `src/UI/sd-ui/src/firebase.js` edit + redeploy).
- [ ] No existing record on `auth.sportdeets.com` that would conflict.

## Execution

### Phase 1 — Add the custom domain to Firebase Hosting

1. Firebase Console → `sportdeets` → **Hosting** (left nav).
2. **Add custom domain** button.
3. Enter `auth.sportdeets.com` → continue.
4. Firebase shows a **TXT record** for domain ownership verification — copy the host + value.
5. Add the TXT record at your DNS provider (typical TTL of 300s for faster verification during setup).
6. Wait for DNS propagation (~5-15 min for most providers; can be longer for some).
7. Click **Verify** in Firebase. Once verified, Firebase shows the next set of records.
8. Firebase will display A and AAAA records to point `auth.sportdeets.com` at Firebase Hosting's IPs. Add both at your DNS provider.
9. Wait for the Firebase Hosting custom domain status to flip from "Setup pending" → "Connected". Firebase auto-provisions a Let's Encrypt-managed SSL certificate at this stage. Typically 1-24 hours.

### Phase 2 — Add to Firebase Auth Authorized Domains

1. Firebase Console → `sportdeets` → **Authentication** → **Settings** tab.
2. **Authorized domains** section → **Add domain**.
3. Add `auth.sportdeets.com` to the allowlist.
4. Save.

(This step prevents `auth/unauthorized-domain` errors when the web client uses the new auth domain.)

### Phase 3 — Cut the web client over

1. In `src/UI/sd-ui/src/firebase.js`, update the `authDomain` field:
   ```diff
   const firebaseConfig = {
     apiKey: "AIzaSyD2z-aIlO1REuGmdiw1Z2kmcUgrpDl4-ko",
   - authDomain: "sportdeets.firebaseapp.com",
   + authDomain: "auth.sportdeets.com",
     projectId: "sportdeets",
     storageBucket: "sportdeets.firebasestorage.app",
     messagingSenderId: "812654295319",
     appId: "1:812654295319:web:bb9e42d84312b00c9a1f52",
   };
   ```
2. Commit, PR, merge, deploy as usual.
3. **Do NOT delete `sportdeets.firebaseapp.com` from the Authorized domains list** — Firebase uses it internally for some flows. Just leave it.

### Phase 4 — Validate

1. Open the web app in a fresh incognito window (clean session, no cached auth state).
2. Click "Sign in with Google".
3. The Google OAuth dialog should now read:

   > Sign in to **sportDeets**
   > to continue to **auth.sportdeets.com**

   (Replacing the previous `sportdeets.firebaseapp.com` on the second line. The App Name from the OAuth consent screen drives the first line.)
4. Complete sign-in. Confirm a session is established and `/user/me` returns the expected user data.
5. Test from a desktop browser + mobile Safari to confirm both flows work.

## Rollback

If anything goes wrong (DNS misconfigured, SSL never provisions, auth flow breaks):

1. Revert the `firebase.js` change — restore `authDomain: "sportdeets.firebaseapp.com"`.
2. Redeploy web.
3. Auth flow goes back to using the Firebase default domain. No user impact.
4. Investigate the custom domain setup at leisure. Re-attempt when ready.

The custom domain entry can stay in Firebase Hosting and Authorized Domains during rollback — it's the `authDomain` field in `firebase.js` that controls which domain the client actually uses.

## Open considerations

- **Mobile app `authDomain`?** The mobile app's `GoogleService-Info.plist` doesn't carry an `authDomain` field — RN-Firebase manages its own URL handling natively, so the custom auth domain is a web-only concern. Mobile sign-in continues to work without any iOS-side change.
- **Email link / passwordless flows.** If you ever enable email-link sign-in (passwordless), the email's continue URL would use the same auth domain. Custom domain → branded email links. Worth keeping in mind, but not blocking.
- **Apple Sign-In Universal Links.** If you ever add Apple Sign-In on iOS, the `aasa` (Apple App Site Association) file for Universal Links would need to be hosted on the same domain. Worth setting up Universal Links separately on its own subdomain (`links.sportdeets.com` or similar) rather than overloading `auth.sportdeets.com`.
- **Cost.** None — Firebase Hosting + custom domain SSL is free at the level we're using.

## Effort estimate

- DNS work + Firebase Hosting custom domain config: ~10 min.
- SSL provisioning wait: 1-24 hours (passive, no work).
- Web client update + deploy: ~5 min once SSL is ready.
- Validation: ~5 min.

Total active time: ~20 minutes. Total wall-clock: ~24 hours.

## Related

- `docs/firebase-project-migration.md` — parent project migration that established the `sportdeets` Firebase project.
