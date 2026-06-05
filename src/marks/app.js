/* sportDeets mark exploration — team dataset + page rendering (vanilla JS) */
(function () {
  'use strict';

  // Curated stress-test set. Weighted toward real two-color teams (the vast
  // majority: all pro leagues + FBS/FCS). One NULL-secondary case is flagged as
  // the rare D2/D3 fallback.
  var TEAMS = [
    // NFL
    { abbr: 'DAL', name: 'Dallas Cowboys', sport: 'NFL', primary: '#003594', secondary: '#B0B7BC' },
    { abbr: 'PIT', name: 'Pittsburgh Steelers', sport: 'NFL', primary: '#000000', secondary: '#FFB612' },
    { abbr: 'CLE', name: 'Cleveland Browns', sport: 'NFL', primary: '#311D00', secondary: '#FF3C00' },
    { abbr: 'LV', name: 'Las Vegas Raiders', sport: 'NFL', primary: '#000000', secondary: '#A5ACAF' },
    { abbr: 'MIA', name: 'Miami Dolphins', sport: 'NFL', primary: '#008E97', secondary: '#FC4C02' },
    { abbr: 'KC', name: 'Kansas City Chiefs', sport: 'NFL', primary: '#E31837', secondary: '#FFB81C' },
    { abbr: 'ARI', name: 'Arizona Cardinals', sport: 'NFL', primary: '#97233F', secondary: '#000000' },
    { abbr: 'SF', name: 'San Francisco 49ers', sport: 'NFL', primary: '#AA0000', secondary: '#B3995D' },
    // MLB
    { abbr: 'NYY', name: 'New York Yankees', sport: 'MLB', primary: '#003087', secondary: '#FFFFFF' },
    { abbr: 'LAD', name: 'Los Angeles Dodgers', sport: 'MLB', primary: '#005A9C', secondary: '#FFFFFF' },
    { abbr: 'SD', name: 'San Diego Padres', sport: 'MLB', primary: '#2F241D', secondary: '#FFC425' },
    { abbr: 'BOS', name: 'Boston Red Sox', sport: 'MLB', primary: '#BD3039', secondary: '#0C2340' },
    // NBA
    { abbr: 'BOS', name: 'Boston Celtics', sport: 'NBA', primary: '#007A33', secondary: '#BA9653' },
    { abbr: 'LAL', name: 'Los Angeles Lakers', sport: 'NBA', primary: '#552583', secondary: '#FDB927' },
    { abbr: 'BKN', name: 'Brooklyn Nets', sport: 'NBA', primary: '#000000', secondary: '#FFFFFF' },
    // NCAA
    { abbr: 'ALA', name: 'Alabama Crimson Tide', sport: 'NCAA', primary: '#9E1B32', secondary: '#FFFFFF' },
    { abbr: 'MICH', name: 'Michigan Wolverines', sport: 'NCAA', primary: '#00274C', secondary: '#FFCB05' },
    { abbr: 'ORE', name: 'Oregon Ducks', sport: 'NCAA', primary: '#154733', secondary: '#FEE123' },
    { abbr: 'ALST', name: 'Alabama State Hornets', sport: 'NCAA', primary: '#E9A900', secondary: '#0A0A0A' },
    { abbr: 'LSU', name: 'LSU Tigers', sport: 'NCAA', primary: '#461D76', secondary: '#FDD023' },
    { abbr: 'TENN', name: 'Tennessee Volunteers', sport: 'NCAA', primary: '#FF8200', secondary: '#FFFFFF' },
    { abbr: 'TEX', name: 'Texas Longhorns', sport: 'NCAA', primary: '#AF5C37', secondary: '#FFFFFF' },
    { abbr: 'MISS', name: 'Ole Miss Rebels', sport: 'NCAA', primary: '#13294B', secondary: '#CF142B' },
    { abbr: 'ADA', name: 'Adams State Grizzlies', sport: 'NCAA', primary: '#000000', secondary: null }
  ];

  // Hero row for each direction: spread across sports + tough cases.
  var HERO = ['DAL|NFL', 'MIA|NFL', 'PIT|NFL', 'NYY|MLB', 'SD|MLB', 'LAL|NBA', 'BKN|NBA', 'MICH|NCAA', 'LSU|NCAA', 'TENN|NCAA', 'TEX|NCAA', 'MISS|NCAA'];
  // Similar-red stress row — the headline differentiation test.
  var REDS = ['KC|NFL', 'ARI|NFL', 'SF|NFL', 'BOS|MLB', 'ALA|NCAA'];
  // Mono / near-black / near-white stress row.
  var EDGE = ['PIT|NFL', 'LV|NFL', 'BKN|NBA', 'NYY|MLB', 'ADA|NCAA'];

  function find(key) {
    var parts = key.split('|');
    return TEAMS.filter(function (t) { return t.abbr === parts[0] && t.sport === parts[1]; })[0];
  }

  function el(tag, cls, html) {
    var n = document.createElement(tag);
    if (cls) n.className = cls;
    if (html != null) n.innerHTML = html;
    return n;
  }

  // A single mark rendered on both a light and a dark tile, with caption.
  function markCell(dirId, team, size, opt) {
    opt = opt || {};
    var cell = el('div', 'cell');
    var tiles = el('div', 'tiles');
    var light = el('div', 'tile tile-light');
    var dark = el('div', 'tile tile-dark');
    light.innerHTML = SDMarks.render(dirId, team, { size: size, theme: 'light' });
    dark.innerHTML = SDMarks.render(dirId, team, { size: size, theme: 'dark' });
    tiles.appendChild(light); tiles.appendChild(dark);
    cell.appendChild(tiles);
    if (!opt.noCaption) {
      var cap = el('div', 'cap');
      cap.innerHTML = '<span class="cap-abbr">' + team.abbr + '</span>' +
        '<span class="cap-sport">' + team.sport + '</span>';
      cell.appendChild(cap);
    }
    return cell;
  }

  function row(dirId, keys, size, opt) {
    var r = el('div', 'mark-row');
    keys.forEach(function (k) {
      var t = find(k); if (t) r.appendChild(markCell(dirId, t, size, opt));
    });
    return r;
  }

  /* ---------- build direction sections ---------------------------------- */
  function buildDirections() {
    var host = document.getElementById('directions');
    SDMarks.directions.forEach(function (d, i) {
      var sec = el('section', 'direction');
      sec.id = 'dir-' + d.id;
      sec.setAttribute('data-screen-label', 'Direction ' + String.fromCharCode(65 + i) + ' · ' + d.name);

      // header
      var head = el('div', 'dir-head');
      head.innerHTML =
        '<div class="dir-index">' + String.fromCharCode(65 + i) + '</div>' +
        '<div class="dir-headtext">' +
        '<h2>' + d.name + '</h2>' +
        '<div class="dir-tag">' + d.tag + '</div>' +
        '</div>';
      sec.appendChild(head);

      var concept = el('p', 'concept', d.concept);
      sec.appendChild(concept);

      // hero renders
      var heroLabel = el('div', 'sub-label', 'Across sports &amp; palettes — light / dark tiles, 72px');
      sec.appendChild(heroLabel);
      sec.appendChild(row(d.id, HERO, 72));

      // grid: reds + edges + meta
      var grid = el('div', 'dir-grid');

      var stress = el('div', 'stress');
      stress.appendChild(el('div', 'sub-label', 'Similar-palette test — five &ldquo;reds&rdquo; must stay distinct'));
      stress.appendChild(row(d.id, REDS, 56));
      stress.appendChild(el('div', 'sub-label', 'Mono / near-black / near-white — must not melt into either tile'));
      stress.appendChild(row(d.id, EDGE, 56));
      // size ramp
      stress.appendChild(el('div', 'sub-label', 'Size ramp — 24 · 32 · 48 · 96 · 128px (dark tile)'));
      var ramp = el('div', 'ramp');
      [24, 32, 48, 96, 128].forEach(function (s) {
        var w = el('div', 'ramp-item');
        w.innerHTML = SDMarks.render(d.id, find('LAD|MLB'), { size: s, theme: 'dark' }) +
          '<span class="ramp-px">' + s + '</span>';
        ramp.appendChild(w);
      });
      stress.appendChild(ramp);
      grid.appendChild(stress);

      // meta panel: pros / cons / multisport
      var meta = el('div', 'meta');
      meta.innerHTML =
        '<div class="meta-block"><div class="meta-h pro">Pros</div><ul>' +
        d.pros.map(function (x) { return '<li>' + x + '</li>'; }).join('') + '</ul></div>' +
        '<div class="meta-block"><div class="meta-h con">Cons</div><ul>' +
        d.cons.map(function (x) { return '<li>' + x + '</li>'; }).join('') + '</ul></div>' +
        '<div class="meta-block"><div class="meta-h ms">Multi-sport</div><p>' + d.multisport + '</p></div>';
      grid.appendChild(meta);

      sec.appendChild(grid);
      host.appendChild(sec);
    });
  }

  /* ---------- playground ------------------------------------------------- */
  var pg = { primary: '#005A9C', secondary: '#FFFFFF', abbr: 'LAD', sport: 'MLB', size: 88, noSec: false };

  function buildPlayground() {
    var presetSel = document.getElementById('pg-preset');
    TEAMS.forEach(function (t, i) {
      var o = el('option'); o.value = i; o.textContent = t.name + '  (' + t.sport + ')';
      presetSel.appendChild(o);
    });
    presetSel.value = '9'; // Dodgers

    function bind(id, fn) { document.getElementById(id).addEventListener('input', fn); }

    presetSel.addEventListener('change', function () {
      var t = TEAMS[+presetSel.value];
      if (!t) return;
      pg.primary = t.primary;
      pg.secondary = t.secondary || '#FFFFFF';
      pg.noSec = !t.secondary;
      pg.abbr = t.abbr; pg.sport = t.sport;
      syncControls(); renderPlayground();
    });
    bind('pg-primary', function (e) { pg.primary = e.target.value; document.getElementById('pg-preset').value = ''; renderPlayground(); });
    bind('pg-secondary', function (e) { pg.secondary = e.target.value; document.getElementById('pg-preset').value = ''; renderPlayground(); });
    bind('pg-abbr', function (e) { pg.abbr = e.target.value.toUpperCase().slice(0, 4); document.getElementById('pg-preset').value = ''; renderPlayground(); });
    bind('pg-size', function (e) { pg.size = +e.target.value; document.getElementById('pg-size-val').textContent = pg.size + 'px'; renderPlayground(); });
    document.getElementById('pg-sport').addEventListener('change', function (e) { pg.sport = e.target.value; document.getElementById('pg-preset').value = ''; renderPlayground(); });
    document.getElementById('pg-nosec').addEventListener('change', function (e) { pg.noSec = e.target.checked; renderPlayground(); });

    syncControls();
    renderPlayground();
  }

  function syncControls() {
    document.getElementById('pg-primary').value = pg.primary;
    document.getElementById('pg-secondary').value = pg.secondary;
    document.getElementById('pg-abbr').value = pg.abbr;
    document.getElementById('pg-sport').value = pg.sport;
    document.getElementById('pg-size').value = pg.size;
    document.getElementById('pg-size-val').textContent = pg.size + 'px';
    document.getElementById('pg-nosec').checked = pg.noSec;
  }

  function renderPlayground() {
    var team = { abbr: pg.abbr || '--', name: 'Custom', sport: pg.sport, primary: pg.primary, secondary: pg.noSec ? null : pg.secondary };
    var host = document.getElementById('pg-output');
    host.innerHTML = '';
    SDMarks.directions.forEach(function (d) {
      var card = el('div', 'pg-card');
      card.innerHTML = '<div class="pg-card-name">' + d.name + '</div>';
      var tiles = el('div', 'tiles');
      var light = el('div', 'tile tile-light');
      var dark = el('div', 'tile tile-dark');
      light.innerHTML = SDMarks.render(d.id, team, { size: pg.size, theme: 'light' });
      dark.innerHTML = SDMarks.render(d.id, team, { size: pg.size, theme: 'dark' });
      tiles.appendChild(light); tiles.appendChild(dark);
      card.appendChild(tiles);
      host.appendChild(card);
    });
    // palette readout
    var p = SDMarks.resolvePalette(pg.primary, pg.noSec ? null : pg.secondary);
    document.getElementById('pg-readout').innerHTML =
      swatch('base', p.baseHex) + swatch('accent', p.accentHex + (p.secProvided ? '' : ' (derived)')) +
      swatch('ink', p.inkHex);
  }
  function swatch(role, hex) {
    var color = hex.split(' ')[0];
    return '<div class="sw"><span class="sw-chip" style="background:' + color + '"></span>' +
      '<span class="sw-role">' + role + '</span><span class="sw-hex">' + hex + '</span></div>';
  }

  /* ---------- in-context preview (matchup card mock) -------------------- */
  function buildContext() {
    var host = document.getElementById('context-rows');
    if (!host) return;
    var matchups = [
      ['NYY|MLB', 'LAD|MLB'], ['ALA|NCAA', 'MICH|NCAA'], ['KC|NFL', 'SF|NFL'], ['LAL|NBA', 'BKN|NBA']
    ];
    SDMarks.directions.forEach(function (d) {
      var col = el('div', 'ctx-col');
      col.appendChild(el('div', 'ctx-col-name', d.name));
      matchups.forEach(function (m) {
        var a = find(m[0]), b = find(m[1]);
        var card = el('div', 'ctx-card');
        card.innerHTML =
          '<div class="ctx-team"><div class="ctx-mk">' + SDMarks.render(d.id, a, { size: 48, theme: 'dark' }) + '</div>' +
          '<div class="ctx-name">' + a.name + '</div></div>' +
          '<div class="ctx-team"><div class="ctx-mk">' + SDMarks.render(d.id, b, { size: 48, theme: 'dark' }) + '</div>' +
          '<div class="ctx-name">' + b.name + '</div></div>';
        col.appendChild(card);
      });
      host.appendChild(col);
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    buildDirections();
    buildContext();
    buildPlayground();
  });
})();
