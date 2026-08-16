# Firebase sign-in-provider overflow blocked new federated users

**Status**: fixed (PR pending) · **Discovered**: 2026-08-16 ·
**Severity**: production outage for every NEW Google/Apple user

## Symptom

Firebase reported users (Google provider) that had no corresponding row in
the API's `User` table. Those people signed in successfully, then every
authenticated API call failed — `/user/me`, `/ui/leagues/discover`,
`/ui/devices`, `/hubs/notifications/negotiate`. Six distinct Firebase UIDs
were affected in the 21 days before discovery, including the contracted
tester cohort engaged to satisfy Play Store review requirements.

Existing users were unaffected, which masked the bug: provisioning only
runs on a lookup miss, so anyone already in the table (e.g. prior-season
testers) never touched the broken path.

## Root cause

`FirebaseAuthenticationMiddleware` has two paths.

The manual token-verification path reads the provider correctly, casting
the decoded token's `firebase` claim to a dictionary and pulling
`sign_in_provider` out of it.

The JWT-Bearer path (`EnhanceAuthenticatedUser`) did this:

```csharp
var provider = context.User.FindFirst("firebase")?.Value
    ?? context.User.FindFirst("sign_in_provider")?.Value
    ?? "unknown";
```

The JWT handler surfaces the token's nested `firebase` object as a single
claim whose value is the **raw JSON**, so `.Value` is the entire blob, not
the provider name. That blob was written to `User.SignInProvider`
(`varchar(100)`):

| Identity | Claim blob | Length | Outcome |
|---|---|---|---|
| password | `{"identities":{"email":["user6@sportdeets.com"]},"sign_in_provider":"password"}` | 79 | fits — row created, provider stored as the blob |
| google.com | `{"identities":{"google.com":["109…"],"email":["…@gmail.com"]},"sign_in_provider":"google.com"}` | 115+ | **overflow** |

Postgres rejected the oversized insert with `22001: value too long for type
character varying(100)`. `GetOrCreateUserAsync` throws on a failed upsert,
so the row was never created and the request failed — on every subsequent
request, forever. Confirming evidence: **zero** rows in `User` had a
provider containing `google`, while several rows literally stored JSON
blobs.

Password users survived only because their blob happens to fit. The margin
was ~42 characters of email address — a real user with a longer address
would have hit the same wall.

## Fix

1. **`FirebaseSignInProviderResolver.Resolve`** (new, unit-tested) parses
   the `firebase` claim JSON and returns `sign_in_provider`, tolerating the
   flattened-dotted-claim and bare-string shapes, and never returning a raw
   object. `EnhanceAuthenticatedUser` now uses it.
2. **`FirebaseSignInProviderResolver.Normalize`** is applied in
   `UpsertUserCommandHandler` before persistence — blank collapses to
   `unknown`, oversized clamps to the column width. A malformed provider
   must degrade the label, never cost us the user row.
3. **Data repair** (`sql/pgsql/repair_signin_provider_blobs.sql`) rewrites
   stored blobs to their real provider.

Affected users self-heal on their next authenticated request once the fix
deploys: the middleware's create path finally succeeds.

## Lessons

- A write failure on a **non-essential attribute** took down the
  **essential entity**. Attribute-level defects should degrade
  attribute-level data; the clamp enforces that.
- The two auth paths had drifted apart — one correct, one not. The shared
  resolver removes the duplication.
- The failure was invisible in aggregate: existing users worked fine, so
  only the Firebase-vs-database count discrepancy exposed it. Worth a
  periodic reconciliation check.
