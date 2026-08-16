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
--
-- `IS JSON OBJECT` (PostgreSQL 16+; this cluster runs 17) guards the cast so a
-- value that merely LOOKS like JSON can't abort the statement — the same
-- malformed-input-must-not-break-things principle the code fix enforces.
-- The extracted value is trimmed, blank-collapsed to 'unknown', and clamped to
-- the column width so this repair can never reintroduce the original defect.
-- Rows whose JSON lacks the key still land on 'unknown' rather than NULL
-- (the column is NOT NULL).
BEGIN;

UPDATE "User"
SET "SignInProvider" = LEFT(
        COALESCE(
            NULLIF(BTRIM("SignInProvider"::jsonb ->> 'sign_in_provider'), ''),
            'unknown'),
        100)
WHERE "SignInProvider" LIKE '{%'
  AND "SignInProvider" IS JSON OBJECT;

COMMIT;

-- 3) Verify: expect no remaining JSON blobs, and a clean provider distribution
-- ("password", "google.com", "apple.com", "unknown").
SELECT "SignInProvider", LENGTH("SignInProvider") AS len, COUNT(*)
FROM "User"
GROUP BY 1, 2
ORDER BY 3 DESC;
