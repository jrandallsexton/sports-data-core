// Phase 1-3 batch generator: reads franchise-colors-mlb.txt (the prod
// query result Randall dropped in src/marks/), renders one roundel SVG
// per row via ../marks.js, rasterizes to a 512x512 PNG via resvg-js,
// and writes the result to ./output/ for visual inspection.
//
// No blob upload, no DB writes — those land in later phases once the
// visual output is approved.

const fs = require('node:fs');
const path = require('node:path');
const { Resvg } = require('@resvg/resvg-js');

const SDMarks = require('../marks.js');

const DATA_FILE = path.resolve(__dirname, '..', 'franchise-colors-mlb.txt');
const OUTPUT_DIR = path.resolve(__dirname, 'output');
const SIZE = 512;
const SPORT = 'MLB'; // every row in the file is MLB-scoped per the query
// All three universal-shape directions from marks.js. Block and equipment
// are excluded — not in the public DIRECTIONS registry and have sport-
// specific branching we explicitly de-scoped.
const DIRECTIONS = ['roundel', 'shield', 'hex'];

// Parse the tab-separated dump. The file is:
//   line 1-5  : the SQL query (commented context for the reader)
//   line 6    : blank
//   line 7    : header row
//   line 8+   : data rows, tab-separated
function readTeams(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  const lines = raw.split(/\r?\n/);

  let headerIdx = lines.findIndex((l) => l.startsWith('FranchiseId\t'));
  if (headerIdx === -1) {
    throw new Error(`Could not locate header row in ${filePath}`);
  }
  const headers = lines[headerIdx].split('\t').map((h) => h.trim());
  const dataLines = lines.slice(headerIdx + 1).filter((l) => l.trim().length > 0);

  return dataLines.map((line) => {
    const cells = line.split('\t');
    const row = {};
    headers.forEach((h, i) => { row[h] = (cells[i] || '').trim(); });
    return row;
  });
}

function ensureDir(p) {
  fs.mkdirSync(p, { recursive: true });
}

function generateOne(direction, team) {
  const svg = SDMarks.render(direction, team, { size: SIZE, theme: 'light' });
  const resvg = new Resvg(svg, {
    fitTo: { mode: 'width', value: SIZE },
    background: 'transparent'
  });
  return resvg.render().asPng();
}

function main() {
  ensureDir(OUTPUT_DIR);
  for (const dir of DIRECTIONS) ensureDir(path.join(OUTPUT_DIR, dir));

  const rows = readTeams(DATA_FILE);
  console.log(`Loaded ${rows.length} teams from ${path.basename(DATA_FILE)}`);
  console.log(`Generating directions: ${DIRECTIONS.join(', ')}`);

  const perDir = Object.fromEntries(DIRECTIONS.map((d) => [d, 0]));
  const failures = [];

  for (const row of rows) {
    // The data file is FranchiseSeason-scoped. Render the mark using the
    // season-level color + abbreviation; we'll add a separate Franchise-
    // level pass once the visual is approved.
    const team = {
      abbr: row.Abbreviation,
      name: row.Slug, // human-readable label in the SVG aria-label
      sport: SPORT,
      primary: '#' + row.ColorCodeHex,
      secondary: row.ColorCodeAltHex ? '#' + row.ColorCodeAltHex : null
    };

    for (const direction of DIRECTIONS) {
      try {
        const png = generateOne(direction, team);
        const outPath = path.join(OUTPUT_DIR, direction, `${row.Slug}.png`);
        fs.writeFileSync(outPath, png);
        perDir[direction]++;
      } catch (err) {
        failures.push({ slug: row.Slug, abbr: row.Abbreviation, direction, error: err.message });
      }
    }
  }

  console.log(`\nWrote PNGs to ${OUTPUT_DIR}:`);
  for (const d of DIRECTIONS) console.log(`  ${d}/  ${perDir[d]} files`);

  if (failures.length) {
    console.log(`\nFailures (${failures.length}):`);
    for (const f of failures) {
      console.log(`  [${f.direction}] ${f.slug} (${f.abbr}): ${f.error}`);
    }
    process.exitCode = 1;
  }
}

main();
