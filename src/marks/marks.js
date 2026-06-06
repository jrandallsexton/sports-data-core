/* ============================================================================
   sportDeets — Parametric Team Mark Engine
   ----------------------------------------------------------------------------
   Given (primary color, secondary color | NULL, abbreviation, sport) produce a
   deterministic, legally-clean SVG. No per-team manual design. No mascots, no
   protected symbology. Just color + geometry + letterform.

   Everything here is pure: same inputs -> same SVG string. That is what lets us
   cache the output to blob storage and serve it from the existing logo URLs.
   ========================================================================== */
(function (global) {
  'use strict';
  // Node-vs-browser shim: attaches to window in the browser (existing behavior)
  // and exports via module.exports under Node (for batch generation scripts).
  // See src/marks/batch/ for the Node consumer.

  /* ---------- color math -------------------------------------------------- */
  function parseHex(c) {
    if (!c) return null;
    c = String(c).trim().replace(/^#/, '');
    if (c.length === 3) c = c.split('').map(function (x) { return x + x; }).join('');
    if (!/^[0-9a-fA-F]{6}$/.test(c)) return null;
    var n = parseInt(c, 16);
    return { r: (n >> 16) & 255, g: (n >> 8) & 255, b: n & 255 };
  }
  function toHex(c) {
    function h(x) { return Math.max(0, Math.min(255, Math.round(x))).toString(16).padStart(2, '0'); }
    return '#' + h(c.r) + h(c.g) + h(c.b);
  }
  function relLum(c) {
    function f(x) { x /= 255; return x <= 0.03928 ? x / 12.92 : Math.pow((x + 0.055) / 1.055, 2.4); }
    return 0.2126 * f(c.r) + 0.7152 * f(c.g) + 0.0722 * f(c.b);
  }
  function contrast(a, b) {
    var l1 = relLum(a), l2 = relLum(b), hi = Math.max(l1, l2), lo = Math.min(l1, l2);
    return (hi + 0.05) / (lo + 0.05);
  }
  function mix(a, b, t) {
    return { r: a.r + (b.r - a.r) * t, g: a.g + (b.g - a.g) * t, b: a.b + (b.b - a.b) * t };
  }
  var WHITE = { r: 255, g: 255, b: 255 };
  var INKBLACK = { r: 18, g: 19, b: 23 };

  // Best-contrast ink (near-white or near-black) for text/shapes on a given bg.
  function inkOn(bg) { return contrast(bg, WHITE) >= contrast(bg, INKBLACK) ? WHITE : INKBLACK; }

  /* ---------- palette resolution -----------------------------------------
     The crux of the whole system. Real data gives us a primary hex and a
     secondary that is OFTEN NULL, OR so close to the primary it is useless as a
     contrast device (e.g. navy primary + black secondary). We must always end
     with three usable roles:
        base   — the team's primary, the dominant fill
        accent — a clearly distinct color for the contrast device (band/ring/
                 split). Derived from the palette when secondary is missing/weak.
        ink    — letter color, max contrast on `base`
     Deterministic: no randomness, pure function of the two input colors.

     ACCENT_CONTRAST_MIN is the threshold for "is this secondary visually
     distinct enough from the primary to use as the accent ring/band." This
     is NOT WCAG text-contrast (4.5/3.0) — that bar applies to text
     legibility and informational graphical elements. The accent here is
     brand-decoration; the bar is "are these two color blocks clearly
     separate?" Set at 1.7 by empirical validation against real MLB color
     data: at 2.5+ the threshold rejects authentic navy+red pairs (Guardians,
     Twins, Braves, Rangers — all ~2.3) and forces a washed-out derived
     mix instead of the actual brand secondary. Tune downward only with new
     visual validation; tune upward only if a navy+near-black case slips
     through.
  ------------------------------------------------------------------------ */
  var ACCENT_CONTRAST_MIN = 1.7;

  function resolvePalette(primaryHex, secondaryHex) {
    var base = parseHex(primaryHex) || { r: 90, g: 95, b: 105 };
    var sec = parseHex(secondaryHex); // may be null
    var baseLight = relLum(base) > 0.42;

    var accent;
    var secUsable = sec && contrast(sec, base) >= ACCENT_CONTRAST_MIN;
    if (secUsable) {
      accent = sec;
    } else {
      // Derive an accent that keeps the team hue but separates tonally.
      accent = baseLight ? mix(base, INKBLACK, 0.62) : mix(base, WHITE, 0.74);
    }
    // Guarantee the accent will actually read against base.
    if (contrast(accent, base) < ACCENT_CONTRAST_MIN) {
      accent = baseLight ? mix(base, INKBLACK, 0.7) : mix(base, WHITE, 0.82);
    }

    var ink = inkOn(base);
    // ink for shapes drawn ON the accent (e.g. knockout letters)
    var inkOnAccent = inkOn(accent);

    return {
      base: base, baseHex: toHex(base),
      sec: sec, secProvided: !!sec,
      accent: accent, accentHex: toHex(accent),
      ink: ink, inkHex: toHex(ink),
      inkOnAccent: inkOnAccent, inkOnAccentHex: toHex(inkOnAccent),
      baseLight: baseLight
    };
  }

  // Hairline that keeps a mark from melting into a same-tone tile.
  function tileKeyline(theme) {
    return theme === 'dark' ? 'rgba(255,255,255,0.16)' : 'rgba(15,17,21,0.14)';
  }

  /* ---------- letterform fitting -----------------------------------------
     Abbreviations run 2–4 chars. We size by length and clamp width with
     textLength so 4-char marks never overflow their shape.
  ------------------------------------------------------------------------ */
  function letter(text, cx, cy, color, opt) {
    opt = opt || {};
    text = String(text || '').toUpperCase();
    var len = text.length || 2;
    var size = opt.size || ({ 1: 56, 2: 52, 3: 40, 4: 30 }[Math.min(len, 4)] || 30);
    var maxW = opt.maxW || 0;
    var attrs = [
      'x="' + cx + '"', 'y="' + cy + '"',
      'text-anchor="middle"', 'dominant-baseline="central"',
      'fill="' + color + '"',
      'font-family="' + (opt.font || "'Archivo','Arial Narrow',sans-serif") + '"',
      'font-weight="' + (opt.weight || 800) + '"',
      'font-size="' + size + '"',
      'letter-spacing="' + (opt.tracking != null ? opt.tracking : (len >= 3 ? -1 : 0)) + '"'
    ];
    if (maxW && len >= 3) {
      attrs.push('textLength="' + maxW + '"', 'lengthAdjust="spacingAndGlyphs"');
    }
    if (opt.stroke) attrs.push('stroke="' + opt.stroke + '"', 'stroke-width="' + (opt.strokeW || 0) + '"', 'paint-order="stroke"');
    return '<text ' + attrs.join(' ') + '>' + esc(text) + '</text>';
  }
  // Escapes for BOTH HTML element content (the <text> body) AND attribute
  // values (the aria-label on <svg>). " and ' aren't needed for element
  // content, but they ARE needed for attribute safety — if a future caller
  // passes a team name that contains a double-quote, the unescaped value
  // would break out of the aria-label attribute. Current callers route only
  // engine-controlled or DB-slug inputs through here, but the escape is
  // cheap defense-in-depth for any future reuse with less-trusted data.
  function esc(s) {
    return String(s).replace(/[<>&"']/g, function (c) {
      return {
        '<': '&lt;',
        '>': '&gt;',
        '&': '&amp;',
        '"': '&quot;',
        "'": '&#39;'
      }[c];
    });
  }

  function svg(inner, opt) {
    opt = opt || {};
    var s = opt.size || 100;
    return '<svg viewBox="0 0 100 100" width="' + s + '" height="' + s + '" ' +
      'xmlns="http://www.w3.org/2000/svg" shape-rendering="geometricPrecision" ' +
      'role="img" aria-label="' + esc(opt.label || '') + '">' + inner + '</svg>';
  }

  /* =========================================================================
     DIRECTION A — MONOGRAM ROUNDEL
     Circle, primary field, inset ring in accent, abbreviation knocked in ink.
     SeatGeek-adjacent. Monogram-forward, universal across all sports.
  ========================================================================= */
  function roundel(team, o) {
    o = o || {}; var p = resolvePalette(team.primary, team.secondary), key = tileKeyline(o.theme);
    var inner = '';
    inner += '<circle cx="50" cy="50" r="47" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<circle cx="50" cy="50" r="40.5" fill="none" stroke="' + p.accentHex + '" stroke-width="5"/>';
    inner += letter(team.abbr, 50, 51.5, p.inkHex, { maxW: 56, weight: 800 });
    return svg(inner, { size: o.size, label: team.name });
  }

  /* =========================================================================
     DIRECTION B — HERALDIC SHIELD
     Crest silhouette, primary field, accent "chief" band across the top
     carrying the abbreviation, optional sport pip in the point.
     Yahoo-Fantasy-adjacent. Classic / heraldic. Abbr supporting.
  ========================================================================= */
  var SHIELD_PATH = 'M16,12 H84 V50 C84,72 68,86 50,93 C32,86 16,72 16,50 Z';
  function shield(team, o) {
    o = o || {}; var p = resolvePalette(team.primary, team.secondary), key = tileKeyline(o.theme);
    var cid = 'sh' + uid();
    var inner = '';
    inner += '<clipPath id="' + cid + '"><path d="' + SHIELD_PATH + '"/></clipPath>';
    inner += '<path d="' + SHIELD_PATH + '" fill="' + p.baseHex + '"/>';
    inner += '<g clip-path="url(#' + cid + ')">';
    inner += '<rect x="14" y="10" width="72" height="26" fill="' + p.accentHex + '"/>';
    inner += sportPip(team.sport, 50, 70, p.inkHex, 0.5);
    inner += '</g>';
    inner += '<path d="' + SHIELD_PATH + '" fill="none" stroke="' + key + '" stroke-width="1.5"/>';
    inner += letter(team.abbr, 50, 24, p.inkOnAccentHex, { maxW: 56, size: ({ 2: 22, 3: 19, 4: 15 }[Math.min(String(team.abbr).length, 4)] || 19), weight: 800 });
    return svg(inner, { size: o.size, label: team.name });
  }

  /* =========================================================================
     DIRECTION C — DIAGONAL SPLIT BLOCK
     Rounded square (squircle), primary field cut by a bold accent sash from
     corner to corner, oversized abbreviation riding the split.
     Sleeper-adjacent but louder. Bold / sporty. Monogram hero.
  ========================================================================= */
  function block(team, o) {
    o = o || {}; var p = resolvePalette(team.primary, team.secondary), key = tileKeyline(o.theme);
    var cid = 'bk' + uid();
    var inner = '';
    inner += '<clipPath id="' + cid + '"><rect x="6" y="6" width="88" height="88" rx="22"/></clipPath>';
    inner += '<g clip-path="url(#' + cid + ')">';
    inner += '<rect x="6" y="6" width="88" height="88" fill="' + p.baseHex + '"/>';
    // diagonal sash (lower-left to upper-right)
    inner += '<polygon points="6,94 30,94 94,30 94,6 70,6 6,70" fill="' + p.accentHex + '"/>';
    inner += '</g>';
    inner += '<rect x="6.75" y="6.75" width="86.5" height="86.5" rx="21.5" fill="none" stroke="' + key + '" stroke-width="1.5"/>';
    inner += letter(team.abbr, 50, 51, p.inkHex, {
      maxW: 60, weight: 900,
      stroke: p.baseHex, strokeW: 3.5
    });
    return svg(inner, { size: o.size, label: team.name });
  }

  /* =========================================================================
     DIRECTION D — SPORT EQUIPMENT FAMILY
     Sport-specific silhouette: football helmet, baseball cap, jersey block
     (basketball/hockey), pennant (golf/other). Generic equipment shapes — no
     team-specific geometry. Primary field, accent stripe, abbr on the face.
  ========================================================================= */
  function equipment(team, o) {
    o = o || {}; var p = resolvePalette(team.primary, team.secondary), key = tileKeyline(o.theme);
    var fam = sportFamily(team.sport);
    if (fam === 'football') return helmet(team, p, key, o);
    if (fam === 'baseball') return cap(team, p, key, o);
    if (fam === 'court') return jersey(team, p, key, o);
    return pennant(team, p, key, o);
  }

  function helmet(team, p, key, o) {
    var cid = 'hm' + uid();
    // Side-profile shell (facing right): the iconic football-helmet silhouette.
    var shell = 'M20,56 C15,37 26,18 48,17 C70,16 84,28 84,47 C84,53 81,57 75,58 L66,58 C64,65 58,68 50,68 L40,68 C29,68 23,63 20,56 Z';
    var inner = '';
    inner += '<clipPath id="' + cid + '"><path d="' + shell + '"/></clipPath>';
    inner += '<path d="' + shell + '" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<g clip-path="url(#' + cid + ')">';
    // center stripe running crown-to-back
    inner += '<path d="M26,30 C40,21 60,21 74,30 L74,40 C60,31 40,31 26,40 Z" fill="' + p.accentHex + '"/>';
    // ear hole
    inner += '<circle cx="44" cy="50" r="6.5" fill="' + p.accentHex + '"/>';
    inner += '<circle cx="44" cy="50" r="2.6" fill="' + p.baseHex + '"/>';
    inner += '</g>';
    // facemask cage projecting from the front (right)
    inner += '<g fill="none" stroke="' + p.accentHex + '" stroke-width="3.4" stroke-linecap="round" stroke-linejoin="round">';
    inner += '<path d="M75,58 C84,62 84,73 75,77 L58,77 C54,77 52,74 52,70"/>';
    inner += '<path d="M74,67 L57,67"/>';
    inner += '</g>';
    inner += letter(team.abbr, 47, 40, p.inkHex, { maxW: 34, size: ({ 2: 19, 3: 15, 4: 12 }[Math.min(String(team.abbr).length, 4)] || 15), weight: 800 });
    return svg(inner, { size: o.size, label: team.name });
  }

  function cap(team, p, key, o) {
    var cid = 'cp' + uid();
    var dome = 'M20,60 C20,33 35,22 50,22 C65,22 80,33 80,60 Z';
    var inner = '';
    inner += '<clipPath id="' + cid + '"><path d="' + dome + '"/></clipPath>';
    inner += '<path d="' + dome + '" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<g clip-path="url(#' + cid + ')">';
    inner += '<rect x="48" y="20" width="4" height="44" fill="' + p.accentHex + '" opacity="0.9"/>';
    inner += '</g>';
    inner += '<circle cx="50" cy="24" r="3" fill="' + p.accentHex + '"/>';
    // brim
    inner += '<path d="M20,60 C20,67 33,72 50,72 C58,72 64,71 68,69 C74,66 78,62 78,60 Z" fill="' + p.accentHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += letter(team.abbr, 50, 47, p.inkHex, { maxW: 40, size: ({ 2: 24, 3: 18, 4: 14 }[Math.min(String(team.abbr).length, 4)] || 18), weight: 800 });
    return svg(inner, { size: o.size, label: team.name });
  }

  function jersey(team, p, key, o) {
    var cid = 'js' + uid();
    var body = 'M30,24 L42,20 C45,25 55,25 58,20 L70,24 L80,34 L72,44 L66,40 L66,82 L34,82 L34,40 L28,44 L20,34 Z';
    var inner = '';
    inner += '<clipPath id="' + cid + '"><path d="' + body + '"/></clipPath>';
    inner += '<path d="' + body + '" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<g clip-path="url(#' + cid + ')">';
    inner += '<rect x="34" y="58" width="32" height="6" fill="' + p.accentHex + '"/>';
    inner += '<rect x="34" y="72" width="32" height="3" fill="' + p.accentHex + '"/>';
    inner += '</g>';
    inner += letter(team.abbr, 50, 47, p.inkHex, { maxW: 28, size: ({ 2: 22, 3: 16, 4: 13 }[Math.min(String(team.abbr).length, 4)] || 16), weight: 900 });
    return svg(inner, { size: o.size, label: team.name });
  }

  function pennant(team, p, key, o) {
    var inner = '';
    var flag = 'M22,24 L82,38 L40,52 L40,84 L22,84 Z';
    inner += '<path d="' + flag + '" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<path d="M22,30 L70,37 L46,45 L22,40 Z" fill="' + p.accentHex + '"/>';
    inner += letter(team.abbr, 33, 66, p.inkHex, { maxW: 22, size: 18, weight: 900 });
    return svg(inner, { size: o.size, label: team.name });
  }

  /* =========================================================================
     DIRECTION E — HEX PATCH (negative-space monogram)
     Pointed hexagon, primary field, inner accent hex, abbreviation KNOCKED OUT
     to the primary color so the letter reads as negative space inside the
     accent. Geometric / modern. Demonstrates the knockout contrast device.
  ========================================================================= */
  var HEX_OUT = '50,5 89,27 89,73 50,95 11,73 11,27';
  var HEX_IN = '50,16 79,32 79,68 50,84 21,68 21,32';
  function hex(team, o) {
    o = o || {}; var p = resolvePalette(team.primary, team.secondary), key = tileKeyline(o.theme);
    var mid = 'hx' + uid();
    var inner = '';
    inner += '<mask id="' + mid + '">';
    inner += '<polygon points="' + HEX_IN + '" fill="#fff"/>';
    inner += letter(team.abbr, 50, 51.5, '#000', { maxW: 46, weight: 900 });
    inner += '</mask>';
    inner += '<polygon points="' + HEX_OUT + '" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<polygon points="' + HEX_IN + '" fill="' + p.accentHex + '" mask="url(#' + mid + ')"/>';
    return svg(inner, { size: o.size, label: team.name });
  }

  /* ---------- sport helpers ---------------------------------------------- */
  function sportFamily(sport) {
    var s = String(sport || '').toLowerCase();
    if (s.indexOf('nfl') >= 0 || s.indexOf('ncaa') >= 0 || s.indexOf('football') >= 0) return 'football';
    if (s.indexOf('mlb') >= 0 || s.indexOf('baseball') >= 0) return 'baseball';
    if (s.indexOf('nba') >= 0 || s.indexOf('nhl') >= 0 || s.indexOf('basket') >= 0 || s.indexOf('hockey') >= 0) return 'court';
    return 'other';
  }
  // Tiny sport glyph (abstract, generic) used as a heraldic pip. Geometric only.
  function sportPip(sport, cx, cy, color, scale) {
    var fam = sportFamily(sport), s = scale || 1;
    var g = '<g transform="translate(' + cx + ',' + cy + ') scale(' + s + ')" fill="none" stroke="' + color + '" stroke-width="3.4" stroke-linecap="round" opacity="0.85">';
    if (fam === 'football') { g += '<ellipse cx="0" cy="0" rx="13" ry="8"/><path d="M-6,0 H6 M-3,-3 V3 M3,-3 V3"/>'; }
    else if (fam === 'baseball') { g += '<circle cx="0" cy="0" r="10"/><path d="M-7,-7 Q0,0 -7,7 M7,-7 Q0,0 7,7"/>'; }
    else if (fam === 'court') { g += '<circle cx="0" cy="0" r="10"/><path d="M-10,0 H10 M0,-10 V10"/>'; }
    else { g += '<circle cx="0" cy="0" r="4" fill="' + color + '"/>'; }
    g += '</g>';
    return g;
  }

  var _u = 0;
  function uid() { return (_u++).toString(36) + Math.random().toString(36).slice(2, 5); }

  /* =========================================================================
     PLAYER AVATAR (roundel-only, single style)
     Same disc + ring + center-text structure as the team roundel, with the
     center content derived from the player. Cascade:
        1. FL initials from the player's DisplayName (default)
        2. Jersey number (fallback when DisplayName can't be parsed)
        3. SCREAM render (hot magenta + "??") when both are unusable
     No user-preference toggle — one render per player. One-off cleanup
     (e.g. "Robert Williams IV" → "RW4") is handled by manually overriding
     the chosen content upstream of this engine.
  ========================================================================= */

  // Hot-magenta scream disc. Stands out in any UI surface (no team in any
  // sport uses pure #FF00FF). The aria-label includes whatever caller-side
  // label we have so debug logs / screen readers can identify which athlete
  // record is broken upstream.
  function screamAvatar(o, label) {
    var inner = '';
    inner += '<circle cx="50" cy="50" r="47" fill="#FF00FF" stroke="#7a007a" stroke-width="1.5"/>';
    inner += letter('??', 50, 51.5, '#ffffff', { maxW: 50, weight: 900 });
    return svg(inner, { size: o.size, label: (label || 'unknown') + ' — missing data' });
  }

  // Derive "FL" initials from a DisplayName. Strips common Roman-numeral and
  // Jr/Sr suffixes, splits on whitespace, takes char-0 of the first token +
  // char-0 of the last token. Returns null when the result wouldn't be at
  // least one character — the caller scream-renders in that case.
  var INITIALS_SUFFIX_RE = /\s+(Jr\.?|Sr\.?|I{1,3}V?|VI{0,3})$/i;
  function deriveInitials(displayName) {
    if (!displayName) return null;
    var s = String(displayName).replace(INITIALS_SUFFIX_RE, '').trim();
    if (!s) return null;
    var tokens = s.split(/\s+/).filter(Boolean);
    if (tokens.length === 0) return null;
    var first = tokens[0].charAt(0);
    if (!first) return null;
    var last = tokens.length > 1 ? tokens[tokens.length - 1].charAt(0) : '';
    return (first + last).toUpperCase();
  }

  function playerAvatar(player, team, o) {
    o = o || {};

    // Cascade: initials → jersey → scream. No style param; one render per
    // player. Upstream callers can short-circuit the cascade by providing a
    // pre-resolved player.contentOverride (used for manual one-off cleanup
    // like "Robert Williams IV" → "RW4" where the auto-derivation would
    // settle for "RW").
    var content = (player && player.contentOverride) || null;
    if (content == null) {
      content = deriveInitials(player && player.displayName);
    }
    if (content == null && player && player.jersey != null && String(player.jersey).length > 0) {
      content = String(player.jersey);
    }
    if (content == null) return screamAvatar(o, player && player.displayName);

    var p = resolvePalette(team.primary, team.secondary);
    var key = tileKeyline(o.theme);
    var inner = '';
    inner += '<circle cx="50" cy="50" r="47" fill="' + p.baseHex + '" stroke="' + key + '" stroke-width="1.5"/>';
    inner += '<circle cx="50" cy="50" r="40.5" fill="none" stroke="' + p.accentHex + '" stroke-width="5"/>';
    inner += letter(content, 50, 51.5, p.inkHex, { maxW: 56, weight: 800 });
    return svg(inner, { size: o.size, label: (player && player.displayName) || '' });
  }

  /* ---------- direction registry ----------------------------------------- */
  var DIRECTIONS = [
    {
      id: 'roundel', name: 'Monogram Roundel', render: roundel,
      tag: 'Clean / modern · monogram-forward · universal',
      concept: 'A primary-color disc with an inset ring in the secondary color and the team abbreviation knocked through the middle. The ring is the contrast device: even two same-red teams differ by their ring color, and a monochrome team gets a derived ring so it never reads as a flat dot. One shape for every sport — zero variant logic.',
      multisport: 'Fully universal. No sport-specific geometry; works identically for NFL, MLB, NBA, NCAA and anything on the roadmap.',
      pros: ['Reads cleanly down to 24px push-notification size', 'Single shape = simplest pipeline, no per-sport branch', 'Ring guarantees a two-tone read even for mono palettes'],
      cons: ['Most "expected" / least ownably-distinct of the set', 'Circles tessellate less interestingly in a 20-row standings list']
    },
    {
      id: 'shield', name: 'Heraldic Shield', render: shield,
      tag: 'Classic / heraldic · abbr supporting · universal (+sport pip)',
      concept: 'A crest silhouette with a secondary-color chief (top band) carrying the abbreviation, and a faint generic sport pip embossed in the field below. Leans into the centuries-old "club crest" language fans already associate with team identity, while staying entirely abstract.',
      multisport: 'Universal silhouette, with an optional abstract sport pip (football/baseball/court) for a subtle hint — drop the pip for a pure-universal variant.',
      pros: ['Feels the most "team/club" of the directions — fans read it as identity, not placeholder', 'Chief band gives a strong, consistent home for the abbreviation', 'The pip adds a sport hint without per-sport silhouettes'],
      cons: ['Shield + small chief text is the hardest to keep legible at 24px', 'Heraldry can read as "old-timey" if the type isn\'t kept modern']
    },
    {
      id: 'hex', name: 'Hex Patch', render: hex,
      tag: 'Geometric / modern · negative-space monogram · universal',
      concept: 'A pointed hexagon with a secondary inner hex, the abbreviation knocked out to the primary color so the letters read as negative space. A patch/badge feel that\'s modern and ownable — the knockout is the contrast device, so the mark can never collapse into one flat color.',
      multisport: 'Fully universal. The hexagon is sport-agnostic and tessellates beautifully in grids.',
      pros: ['Distinct silhouette — not a circle, square or shield', 'Knockout guarantees internal contrast by construction', 'Patch/badge language feels like a deliberate brand system'],
      cons: ['Negative-space letters need a large-enough inner area; 4-char is tight', 'Two nested hexes can muddy at the very smallest sizes']
    }
  ];

  /* ---------- public api -------------------------------------------------- */
  var API = {
    parseHex: parseHex, toHex: toHex, contrast: contrast, relLum: relLum,
    resolvePalette: resolvePalette, sportFamily: sportFamily,
    directions: DIRECTIONS,
    byId: function (id) { return DIRECTIONS.filter(function (d) { return d.id === id; })[0]; },
    render: function (id, team, opt) {
      var d = this.byId(id);
      return d ? d.render(team, opt || {}) : '';
    },
    // Player avatars — single style, cascade initials → jersey → scream.
    // See playerAvatar() above for the cascade details and the
    // contentOverride escape hatch for manual one-off cleanup.
    renderPlayer: function (player, team, opt) {
      return playerAvatar(player, team, opt || {});
    },
    deriveInitials: deriveInitials
  };
  global.SDMarks = API;
  if (typeof module === 'object' && module.exports) module.exports = API;
})(typeof window !== 'undefined' ? window : globalThis);
