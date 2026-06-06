// Hand-curated player-avatar samples for visual sign-off before we wire
// the real-data batch. Each sample exercises a step in the cascade:
// initials (default) → jersey (fallback) → scream (both unusable).
// Writes PNGs to ./output/players-demo/.

const fs = require('node:fs');
const path = require('node:path');
const { Resvg } = require('@resvg/resvg-js');

const SDMarks = require('../marks.js');

const OUTPUT_DIR = path.resolve(__dirname, 'output', 'players-demo');
const SIZE = 512;

// Real team color data pulled from our committed franchise-colors-mlb.txt.
const TEAMS = {
  yankees:  { primary: '#132448', secondary: '#c4ced4' },
  padres:   { primary: '#2f241d', secondary: '#ffc425' },
  dodgers:  { primary: '#005a9c', secondary: '#ffffff' },
  mets:     { primary: '#002d72', secondary: '#ff5910' },
  braves:   { primary: '#0c2340', secondary: '#ba0c2f' },
  giants:   { primary: '#000000', secondary: '#fd5a1e' }
};

// Six samples covering the cascade:
//   1. Normal name → initials win (the default path)
//   2. Apostrophe last name → initials win, apostrophe correctly stripped
//   3. Roman-numeral suffix → initials win, suffix correctly stripped
//   4. Blank name but valid jersey → jersey fallback
//   5. Manual contentOverride → bypasses the cascade entirely
//   6. Blank name AND no jersey → SCREAM
const SAMPLES = [
  {
    label: 'normal name — initials win',
    player: { displayName: 'Aaron Judge', jersey: 99 },
    team: TEAMS.yankees
  },
  {
    label: 'apostrophe last name — initials win',
    player: { displayName: "Brandon O'Brien", jersey: 17 },
    team: TEAMS.padres
  },
  {
    label: 'Jr suffix stripped — initials win',
    player: { displayName: 'Ken Griffey Jr.', jersey: 24 },
    team: TEAMS.mets
  },
  {
    label: 'blank name — jersey fallback',
    player: { displayName: '', jersey: 42 },
    team: TEAMS.dodgers
  },
  {
    label: 'manual override — RW4 wins',
    player: { displayName: 'Robert Williams IV', jersey: 33, contentOverride: 'RW4' },
    team: TEAMS.braves
  },
  {
    label: 'blank name AND no jersey — SCREAM',
    player: { displayName: '', jersey: null },
    team: TEAMS.giants
  }
];

function ensureDir(p) { fs.mkdirSync(p, { recursive: true }); }

function renderPng(player, team) {
  const svg = SDMarks.renderPlayer(player, team, { size: SIZE, theme: 'light' });
  return new Resvg(svg, { fitTo: { mode: 'width', value: SIZE }, background: 'transparent' })
    .render().asPng();
}

function safeFilename(s) {
  return s.replace(/[^a-z0-9]+/gi, '-').toLowerCase();
}

function main() {
  ensureDir(OUTPUT_DIR);
  console.log(`Writing ${SAMPLES.length} player-avatar samples to ${OUTPUT_DIR}`);

  for (const sample of SAMPLES) {
    const png = renderPng(sample.player, sample.team);
    const name = `${safeFilename(sample.label)}.png`;
    fs.writeFileSync(path.join(OUTPUT_DIR, name), png);
    console.log(`  ${name}  (${sample.label})`);
  }
}

main();
