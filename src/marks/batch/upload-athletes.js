// Athlete avatar batch — renders one avatar per athlete via the cascade
// (initials → jersey → scream), uploads to Azure Blob, writes a manifest
// for insert-athletes.js to consume.
//
// Reads a per-sport athletes-{sport}.txt data file produced by executing
// src/marks/athletes.sql against the per-sport Producer DB.

const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const { Resvg } = require('@resvg/resvg-js');
const { BlobServiceClient } = require('@azure/storage-blob');

const SDMarks = require('../marks.js');

const SPORT = process.env.SD_SPORT || 'MLB';
const DATA_FILE = path.resolve(
  __dirname,
  '..',
  process.env.SD_DATA_FILE || 'athletes-mlb.txt'
);
const SCOPE = process.env.SD_SCOPE || 'athletes';
const OUTPUT_DIR = path.resolve(__dirname, 'output');
const MANIFEST_DIR = path.join(OUTPUT_DIR, 'manifests');
const CONTAINER = 'sportdeets-marks';
const SIZE = 512;

function readAthletes(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const lines = raw.split(/\r?\n/);
  const headerIdx = lines.findIndex((l) => l.startsWith('AthleteId\t'));
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

function syntheticHash(athleteId) {
  return crypto.createHash('sha256')
    .update(`sportdeets-mark:athlete:${athleteId}`)
    .digest('hex');
}

function renderPng(player, team) {
  const svg = SDMarks.renderPlayer(player, team, { size: SIZE, theme: 'light' });
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
  await container.createIfNotExists({ access: 'blob' });

  const rows = readAthletes(DATA_FILE);
  console.log(`Loaded ${rows.length} athletes from ${path.basename(DATA_FILE)}`);
  console.log(`Target: ${targetEnv} / container "${CONTAINER}" / scope ${SCOPE}`);
  console.log(`Uploading ${rows.length} avatars...\n`);

  const entries = [];
  const failures = [];
  // Diagnostic counters — surface the cascade distribution at the end so we
  // can see how much of the dataset fell through to jersey or screamed.
  let initialsCount = 0, jerseyCount = 0, screamCount = 0;

  for (let i = 0; i < rows.length; i++) {
    const row = rows[i];

    // Normalize the data-file's literal "NULL" / empty for jersey + colors.
    const jerseyRaw = row.Jersey;
    const jersey = (jerseyRaw && jerseyRaw !== 'NULL') ? jerseyRaw : null;
    const altHex = row.Secondary;

    const player = {
      displayName: row.DisplayName,
      jersey: jersey
    };
    const team = {
      primary: '#' + row.Primary,
      secondary: (altHex && altHex !== 'NULL') ? '#' + altHex : null
    };

    // Determine which cascade branch this athlete will land in — useful
    // diagnostic without re-running the engine on the rendered output.
    const initials = SDMarks.deriveInitials(player.displayName);
    let branch;
    if (initials != null) { branch = 'initials'; initialsCount++; }
    else if (jersey != null) { branch = 'jersey'; jerseyCount++; }
    else { branch = 'scream'; screamCount++; }

    const blobPath = `athlete/${row.AthleteId}.png`;
    try {
      const png = renderPng(player, team);
      const blob = container.getBlockBlobClient(blobPath);
      await blob.uploadData(png, {
        blobHTTPHeaders: { blobContentType: 'image/png' }
      });
      entries.push({
        kind: 'athlete',
        branch,
        entityId: row.AthleteId,
        displayName: row.DisplayName,
        jersey: jersey,
        teamSlug: row.TeamSlug,
        sport: SPORT,
        blobPath,
        blobUrl: blob.url,
        originalUrlHash: syntheticHash(row.AthleteId),
        width: SIZE,
        height: SIZE,
        rel: ['sportdeets-mark']
      });
      // Sparse progress for long runs — one dot per 50 athletes is enough
      // to know it's alive without flooding the console.
      if ((i + 1) % 50 === 0) process.stdout.write('.');
    } catch (err) {
      failures.push({ athleteId: row.AthleteId, displayName: row.DisplayName, error: err.message });
      process.stdout.write('x');
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
    cascade: { initials: initialsCount, jersey: jerseyCount, scream: screamCount },
    entries
  };
  const ts = new Date().toISOString().replace(/[:.]/g, '-');
  const manifestPath = path.join(MANIFEST_DIR, `athletes-${SPORT.toLowerCase()}-${ts}.json`);
  fs.writeFileSync(manifestPath, JSON.stringify(manifest, null, 2));

  console.log(`Uploaded ${entries.length} blobs (${failures.length} failures)`);
  console.log(`Cascade: ${initialsCount} initials, ${jerseyCount} jersey, ${screamCount} scream`);
  console.log(`Manifest: ${manifestPath}`);
  if (failures.length) {
    console.log('\nFailures:');
    for (const f of failures.slice(0, 20)) {
      console.log(`  ${f.athleteId} (${f.displayName}): ${f.error}`);
    }
    if (failures.length > 20) console.log(`  ... and ${failures.length - 20} more`);
    process.exitCode = 1;
  }
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
