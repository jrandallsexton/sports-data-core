// Phase 5: read a manifest produced by upload.js and write logo rows into
// Postgres. Idempotent — re-runnable against the same manifest.
//
// Handles both manifest grains by entry.kind:
//   'franchise-season' -> FranchiseSeasonLogo (keyed by FranchiseSeasonId)
//   'franchise'        -> FranchiseLogo       (keyed by FranchiseId)
//
// Usage:
//   node insert.js                # uses the newest manifest in output/manifests/
//   node insert.js path/to/manifest.json
//
// Idempotency: SELECT by (<fkColumn>, OriginalUrlHash); UPDATE if a row already
// exists, INSERT otherwise. No schema change required.

// Maps a manifest entry.kind to its target table + foreign-key column. Values
// are fixed constants (never user input), so interpolating them into SQL is safe.
const LOGO_TARGETS = {
  'franchise-season': { table: 'FranchiseSeasonLogo', fkColumn: 'FranchiseSeasonId' },
  'franchise': { table: 'FranchiseLogo', fkColumn: 'FranchiseId' },
};

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

// Idempotent upsert of one mark row into the given logo table. Returns
// 'inserted' or 'updated'. `table`/`fkColumn` come from LOGO_TARGETS (fixed
// constants), so string-interpolating them is safe.
async function upsertLogo(client, table, fkColumn, fkValue, e) {
  const existing = await client.query(
    `SELECT "Id" FROM "${table}"
     WHERE "${fkColumn}" = $1 AND "OriginalUrlHash" = $2
     LIMIT 1`,
    [fkValue, e.originalUrlHash]
  );

  if (existing.rowCount > 0) {
    await client.query(
      `UPDATE "${table}"
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
    return 'updated';
  }

  await client.query(
    `INSERT INTO "${table}"
       ("Id", "${fkColumn}", "Uri", "OriginalUrlHash",
        "Width", "Height", "Rel", "IsForDarkBg",
        "CreatedUtc", "CreatedBy", "ModifiedUtc", "ModifiedBy")
     VALUES
       ($1, $2, $3, $4,
        $5, $6, $7, $8,
        (NOW() AT TIME ZONE 'UTC'), $9, NULL, NULL)`,
    [
      crypto.randomUUID(),
      fkValue,
      e.blobUrl,
      e.originalUrlHash,
      e.width,
      e.height,
      e.rel,
      false,
      CREATED_BY
    ]
  );
  return 'inserted';
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

  let inserted = 0, updated = 0, failed = 0, skipped = 0;

  for (const e of manifest.entries) {
    const target = LOGO_TARGETS[e.kind];
    if (!target) {
      // Unknown grain — count it rather than silently dropping.
      skipped++;
      process.stdout.write('s');
      continue;
    }
    try {
      const result = await upsertLogo(client, target.table, target.fkColumn, e.entityId, e);
      if (result === 'inserted') { inserted++; process.stdout.write('i'); }
      else { updated++; process.stdout.write('u'); }
    } catch (err) {
      failed++;
      process.stdout.write('x');
      console.error(`\n[${e.direction}] ${e.slug}: ${err.message}`);
    }
  }
  process.stdout.write('\n\n');

  await client.end();

  console.log(`Inserted: ${inserted}  Updated: ${updated}  Skipped: ${skipped}  Failed: ${failed}`);
  if (failed > 0) process.exitCode = 1;
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
