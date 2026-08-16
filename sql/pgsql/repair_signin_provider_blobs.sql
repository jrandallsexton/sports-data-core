-- Repair User.SignInProvider values that stored the entire Firebase `firebase`
-- claim JSON instead of the provider name.
-- See docs/audit/firebase-signin-provider-overflow.md.
--
-- Target DB: sdApi.All
-- Safe to run before or after the code fix deploys; it is idempotent and only
-- touches rows whose provider is a JSON object.

-- 1) Inspect first.
SELECT "Id",
       "Email",
       LENGTH("SignInProvider") AS provider_len,
       "SignInProvider"
FROM "User"
WHERE "SignInProvider" LIKE '{%'
ORDER BY provider_len DESC;

-- 2) Repair: extract sign_in_provider from the stored JSON.
-- Rows whose JSON lacks the key (or won't parse) are left untouched by the
-- WHERE clause below so nothing is silently blanked.
BEGIN;

UPDATE "User"
SET "SignInProvider" = ("SignInProvider"::jsonb ->> 'sign_in_provider')
WHERE "SignInProvider" LIKE '{%'
  AND ("SignInProvider"::jsonb ->> 'sign_in_provider') IS NOT NULL;

COMMIT;

-- 3) Verify: expect no remaining JSON blobs, and a clean provider distribution
-- ("password", "google.com", "apple.com", "unknown").
SELECT "SignInProvider", LENGTH("SignInProvider") AS len, COUNT(*)
FROM "User"
GROUP BY 1, 2
ORDER BY 3 DESC;
