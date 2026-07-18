// Phase 4: render every (team x direction) mark and upload to Azure Blob.
// Writes a manifest to output/manifests/ describing what was uploaded so the
// insert phase can run independently (or be re-run safely).
//
// Reads the franchise-colors-mlb.txt file as the input dataset for this first
// pass. Future runs will query Postgres directly.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { Resvg } = require('@resvg/resvg-js');
const { BlobServiceClient } = require('@azure/storage-blob');

const SDMarks = require('../marks.js');

// Per-run inputs come from env vars set by run.ps1 (SD_SPORT / SD_DATA_FILE),
// with MLB defaults so the first-pass developer experience still works
// without the wrapper.
const SPORT = process.env.SD_SPORT || 'MLB';
// Data files live in batch/data/. SD_DATA_FILE is a bare filename resolved
// there (run.ps1 sets it per sport, e.g. franchise-colors-ncaafb.txt).
const DATA_FILE = path.resolve(
  __dirname,
  'data',
  process.env.SD_DATA_FILE || 'franchise-colors-mlb.txt'
);
// Default matches the year currently in franchise-colors.sql; run.ps1 sets
// SD_SCOPE explicitly so this default only matters for direct node invocation.
const SCOPE = process.env.SD_SCOPE || 'franchise-season:2026';
// Entity grain this run targets. 'franchise-season' (default, legacy) keys marks
// by FranchiseSeasonId; 'franchise' (the go-forward backfill) keys them by
// FranchiseId — one year-invariant mark per franchise. The franchise-grain data
// file (franchise-colors.sql) carries FranchiseId but no FranchiseSeasonId.
const KIND = process.env.SD_KIND || 'franchise-season';
const OUTPUT_DIR = path.resolve(__dirname, 'output');
const MANIFEST_DIR = path.join(OUTPUT_DIR, 'manifests');
const CONTAINER = 'sportdeets-marks';
const SIZE = 512;
const DIRECTIONS = ['roundel', 'shield', 'hex'];

function readTeams(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const lines = raw.split(/\r?\n/);
  const headerIdx = lines.findIndex((l) => l.startsWith('FranchiseId\t'));
  if (headerIdx === -1) throw new Error(`Could not locate header row in ${filePath}`);
  const headers = lines[headerIdx].split('\t').map((h) => h.trim());
  const dataLines = lines.slice(headerIdx + 1).filter((l) => l.trim().length > 0);
  return dataLines.map((line) => {
    const cells = line.split('\t');
    const row = {};
    headers.forEach((h, i) => { row[h] = (cells[i] || '').trim(); });
    return row;
  });
}

function ensureDir(p) { fs.mkdirSync(p, { recursive: true }); }

function syntheticHash(direction, id) {
  return crypto.createHash('sha256')
    .update(`sportdeets-mark:${direction}:${id}`)
    .digest('hex');
}

function renderPng(direction, team) {
  const svg = SDMarks.render(direction, team, { size: SIZE, theme: 'light' });
  const resvg = new Resvg(svg, {
    fitTo: { mode: 'width', value: SIZE },
    background: 'transparent'
  });
  return resvg.render().asPng();
}

async function main() {
  const conn = process.env.AZURE_BLOB_CONNECTION_STRING;
  if (!conn) {
    console.error('AZURE_BLOB_CONNECTION_STRING is not set. Run via ./run.ps1.');
    process.exit(1);
  }
  const targetEnv = process.env.SD_TARGET_ENV || 'unknown';

  ensureDir(OUTPUT_DIR);
  ensureDir(MANIFEST_DIR);

  const blobService = BlobServiceClient.fromConnectionString(conn);
  const container = blobService.getContainerClient(CONTAINER);
  // PublicAccessType.BlobContainer matches the existing convention in
  // BlobStorageProvider.cs so anonymous GETs work the same as ESPN-sourced
  // logos do today.
  await container.createIfNotExists({ access: 'blob' });

  const rows = readTeams(DATA_FILE);
  console.log(`Loaded ${rows.length} teams from ${path.basename(DATA_FILE)}`);
  console.log(`Target: ${targetEnv} / container "${CONTAINER}" / scope ${SCOPE}`);
  console.log(`Uploading ${rows.length * DIRECTIONS.length} blobs...\n`);

  const entries = [];
  const failures = [];

  for (const row of rows) {
    // NCAAFB data carries the literal string "NULL" (not an empty cell) for
    // teams without a secondary color. Treat that as a real null so the
    // engine's derived-accent path kicks in.
    const altHex = row.ColorCodeAltHex;
    const team = {
      abbr: row.Abbreviation,
      name: row.Slug,
      sport: SPORT,
      primary: '#' + row.ColorCodeHex,
      secondary: (altHex && altHex !== 'NULL') ? '#' + altHex : null
    };

    // The entity a mark is keyed to depends on the run grain: FranchiseId for a
    // franchise-level run, FranchiseSeasonId for a season-level one. The blob
    // path and idempotency hash both key off it, so re-runs are stable per grain.
    const entityId = KIND === 'franchise' ? row.FranchiseId : row.FranchiseSeasonId;

    for (const direction of DIRECTIONS) {
      const blobPath = `${KIND}/${direction}/${entityId}.png`;
      try {
        const png = renderPng(direction, team);
        const blob = container.getBlockBlobClient(blobPath);
        await blob.uploadData(png, {
          blobHTTPHeaders: { blobContentType: 'image/png' }
        });
        entries.push({
          kind: KIND,
          direction,
          entityId,
          franchiseId: row.FranchiseId,
          slug: row.Slug,
          sport: SPORT,
          blobPath,
          blobUrl: blob.url,
          originalUrlHash: syntheticHash(direction, entityId),
          width: SIZE,
          height: SIZE,
          rel: ['sportdeets-mark', direction]
        });
        process.stdout.write('.');
      } catch (err) {
        failures.push({ slug: row.Slug, direction, error: err.message });
        process.stdout.write('x');
      }
    }
  }
  process.stdout.write('\n\n');

  const manifest = {
    generatedAt: new Date().toISOString(),
    environment: targetEnv,
    container: CONTAINER,
    sport: SPORT,
    scope: SCOPE,
    sourceFile: path.basename(DATA_FILE),
    entries
  };
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  const manifestPath = path.join(MANIFEST_DIR, `manifest-${ts}.json`);
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2));

  console.log(`Uploaded ${entries.length} blobs (${failures.length} failures)`);
  console.log(`Manifest: ${manifestPath}`);
  if (failures.length) {
    console.log('\nFailures:');
    for (const f of failures) console.log(`  [${f.direction}] ${f.slug}: ${f.error}`);
    process.exitCode = 1;
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
