// Read an athlete manifest produced by upload-athletes.js and write
// AthleteImage rows into Postgres. Idempotent — re-runnable against the
// same manifest. Mirrors insert.js (team/franchise version) but writes to
// AthleteImage with Rel = ["sportdeets-mark"] (no direction sub-tag).
//
// Usage:
//   node insert-athletes.js                # uses the newest athletes-* manifest
//   node insert-athletes.js path/to/manifest.json

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { Client } = require('pg');

const MANIFEST_DIR = path.resolve(__dirname, 'output', 'manifests');
const CREATED_BY = '11111111-1111-1111-1111-111111111111';

function latestAthleteManifest() {
  if (!fs.existsSync(MANIFEST_DIR)) return null;
  const files = fs.readdirSync(MANIFEST_DIR)
    .filter((f) => f.startsWith('athletes-') && f.endsWith('.json'))
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
    : latestAthleteManifest();
  if (!manifestPath || !fs.existsSync(manifestPath)) {
    console.error(`Manifest not found. Pass a path or place one in ${MANIFEST_DIR}`);
    process.exit(1);
  }

  console.log(`Manifest: ${manifestPath}`);
  const manifest = JSON.parse(fs.readFileSync(manifestPath, 'utf8'));
  console.log(`Env: ${manifest.environment}  Scope: ${manifest.scope}  Entries: ${manifest.entries.length}`);
  if (manifest.cascade) {
    console.log(`Cascade: ${manifest.cascade.initials} initials, ${manifest.cascade.jersey} jersey, ${manifest.cascade.scream} scream`);
  }

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

  for (let i = 0; i < manifest.entries.length; i++) {
    const e = manifest.entries[i];
    if (e.kind !== 'athlete') continue;

    try {
      const existing = await client.query(
        `SELECT "Id" FROM "AthleteImage"
         WHERE "AthleteId" = $1 AND "OriginalUrlHash" = $2
         LIMIT 1`,
        [e.entityId, e.originalUrlHash]
      );

      if (existing.rowCount > 0) {
        await client.query(
          `UPDATE "AthleteImage"
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
      } else {
        await client.query(
          `INSERT INTO "AthleteImage"
             ("Id", "AthleteId", "Uri", "OriginalUrlHash",
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
      }
      if ((i + 1) % 100 === 0) process.stdout.write('.');
    } catch (err) {
      failed++;
      console.error(`\n${e.entityId} (${e.displayName}): ${err.message}`);
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
