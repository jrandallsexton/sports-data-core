// Phase 5: read a manifest produced by upload.js and write FranchiseSeasonLogo
// rows into Postgres. Idempotent — re-runnable against the same manifest.
//
// Usage:
//   node insert.js                # uses the newest manifest in output/manifests/
//   node insert.js path/to/manifest.json
//
// Idempotency: SELECT by (FranchiseSeasonId, OriginalUrlHash); UPDATE if a
// row already exists, INSERT otherwise. No schema change required.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { Client } = require('pg');

const MANIFEST_DIR = path.resolve(__dirname, 'output', 'manifests');
// The "sportDeets admin" user; used for audit columns where there is no
// human actor (this batch runs server-side, not on behalf of a logged-in
// user).
const CREATED_BY = '11111111-1111-1111-1111-111111111111';

function latestManifest() {
  if (!fs.existsSync(MANIFEST_DIR)) return null;
  const files = fs.readdirSync(MANIFEST_DIR)
    .filter((f) => f.startsWith('manifest-') && f.endsWith('.json'))
    .sort();
  return files.length ? path.join(MANIFEST_DIR, files[files.length - 1]) : null;
}

function requireEnv(name) {
  const v = process.env[name];
  if (!v) {
    console.error(`${name} is not set. Run via ./run.ps1.`);
    process.exit(1);
  }
  return v;
}

async function main() {
  const manifestArg = process.argv[2];
  const manifestPath = manifestArg
    ? path.resolve(manifestArg)
    : latestManifest();
  if (!manifestPath || !fs.existsSync(manifestPath)) {
    console.error(`Manifest not found. Pass a path or place one in ${MANIFEST_DIR}`);
    process.exit(1);
  }

  console.log(`Manifest: ${manifestPath}`);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  console.log(`Env: ${manifest.environment}  Scope: ${manifest.scope}  Entries: ${manifest.entries.length}`);

  const client = new Client({
    host: requireEnv('PG_HOST'),
    port: parseInt(process.env.PG_PORT || '5432', 10),
    user: requireEnv('PG_USER'),
    password: requireEnv('PG_PASSWORD'),
    database: requireEnv('PG_DATABASE')
  });

  await client.connect();
  console.log(`Connected to ${process.env.PG_DATABASE} @ ${process.env.PG_HOST}\n`);

  let inserted = 0, updated = 0, failed = 0;

  for (const e of manifest.entries) {
    if (e.kind !== 'franchise-season') {
      // Franchise-level rows will land in a later batch pass.
      continue;
    }
    try {
      const existing = await client.query(
        `SELECT "Id" FROM "FranchiseSeasonLogo"
         WHERE "FranchiseSeasonId" = $1 AND "OriginalUrlHash" = $2
         LIMIT 1`,
        [e.entityId, e.originalUrlHash]
      );

      if (existing.rowCount > 0) {
        await client.query(
          `UPDATE "FranchiseSeasonLogo"
           SET "Uri" = $1,
               "Width" = $2,
               "Height" = $3,
               "Rel" = $4,
               "IsForDarkBg" = $5,
               "ModifiedUtc" = (NOW() AT TIME ZONE 'UTC'),
               "ModifiedBy" = $6
           WHERE "Id" = $7`,
          [e.blobUrl, e.width, e.height, e.rel, false, CREATED_BY, existing.rows[0].Id]
        );
        updated++;
        process.stdout.write('u');
      } else {
        await client.query(
          `INSERT INTO "FranchiseSeasonLogo"
             ("Id", "FranchiseSeasonId", "Uri", "OriginalUrlHash",
              "Width", "Height", "Rel", "IsForDarkBg",
              "CreatedUtc", "CreatedBy", "ModifiedUtc", "ModifiedBy")
           VALUES
             ($1, $2, $3, $4,
              $5, $6, $7, $8,
              (NOW() AT TIME ZONE 'UTC'), $9, NULL, NULL)`,
          [
            crypto.randomUUID(),
            e.entityId,
            e.blobUrl,
            e.originalUrlHash,
            e.width,
            e.height,
            e.rel,
            false,
            CREATED_BY
          ]
        );
        inserted++;
        process.stdout.write('i');
      }
    } catch (err) {
      failed++;
      process.stdout.write('x');
      console.error(`\n[${e.direction}] ${e.slug}: ${err.message}`);
    }
  }
  process.stdout.write('\n\n');

  await client.end();

  console.log(`Inserted: ${inserted}  Updated: ${updated}  Failed: ${failed}`);
  if (failed > 0) process.exitCode = 1;
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
