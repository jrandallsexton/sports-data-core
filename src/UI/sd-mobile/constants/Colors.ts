// ─── Theme palette ────────────────────────────────────────────────────────────
//
// Values mirror the CSS custom properties in sd-ui/src/App.css so the mobile
// app and web app feel like the same product in light and dark mode. Keep the
// two in sync when either palette shifts — see docs/mobile/mobile-app-overview.md
// under "Code Sharing" for why a shared-tokens package is the long-term fix.
//
// Notes:
// - `tint` is the active accent (primary action, focused state, link color).
//   It corresponds to web's `--accent`. Light uses `#0077cc`; dark uses
//   `#61dafb` (the React cyan). This is what makes the theme recognizable.
// - `brand.navy` / `brand.gold` are kept as static legacy tokens for team
//   color fallbacks (InsightModal, MatchupCard, StatsComparisonModal) where
//   the design intent is "a neutral pop color when the team color isn't
//   available" — not a themed accent. Remove once those call sites migrate.

/** @deprecated — use theme-aware tokens from `getTheme()` instead.
 *  Retained for team-color fallbacks that shouldn't shift with theme. */
export const brand = {
  navy: '#1B3A6B',
  navyLight: '#2A5298',
  gold: '#D4AF37',
  goldLight: '#F0D060',
};

// Shared status color semantics — per scheme values set below.
export type ColorScheme = 'light' | 'dark';

const light = {
  // Backgrounds
  background: '#f5f7fa',        // --bg-primary
  backgroundSecondary: '#e9ecef',
  card: '#ffffff',              // --bg-card
  cardHover: '#f1f1f1',
  elevated: '#ffffff',
  modal: '#ffffff',
  overlay: 'rgba(0, 0, 0, 0.5)',

  // Text
  text: '#1a1a1a',              // --text-primary
  textSecondary: '#555',
  textMuted: '#888',
  textOnAccent: '#ffffff',
  textLink: '#0077cc',

  // Accent / brand (theme-aware)
  tint: '#0077cc',              // --accent
  tintHover: '#005fa3',
  accentMuted: 'rgba(0, 119, 204, 0.15)',
  accentSubtle: 'rgba(0, 119, 204, 0.06)',

  // Borders
  border: '#ddd',
  borderSubtle: '#e9ecef',
  borderStrong: '#ccc',

  // Tabs / separators
  tabIconDefault: '#888',
  tabIconSelected: '#0077cc',
  separator: '#e9ecef',

  // Status
  success: '#1b5e20',
  successText: '#1b5e20',
  successBg: 'rgba(40, 167, 69, 0.1)',
  error: '#b71c1c',
  errorText: '#b71c1c',
  errorBg: 'rgba(220, 53, 69, 0.1)',
  warning: '#ff8c00',
  warningText: '#e65100',
  info: '#0077cc',

  // Domain
  pickCorrect: '#1b5e20',
  pickIncorrect: '#b71c1c',
  spreadLine: '#e65100',

  // Shadows
  shadowColor: '#000',
};

const dark = {
  background: '#111',
  backgroundSecondary: '#1a1a1a',
  card: '#222',
  cardHover: '#2a2d33',
  elevated: '#282c34',
  modal: '#1e2127',
  overlay: 'rgba(0, 0, 0, 0.7)',

  text: '#f8f9fa',
  textSecondary: '#adb5bd',
  textMuted: '#6c757d',
  textOnAccent: '#111',
  textLink: '#61dafb',

  tint: '#61dafb',
  tintHover: '#4ea0d9',
  accentMuted: 'rgba(97, 218, 251, 0.2)',
  accentSubtle: 'rgba(97, 218, 251, 0.08)',

  border: '#333',
  borderSubtle: '#2a2d33',
  borderStrong: '#444',

  tabIconDefault: '#6c757d',
  tabIconSelected: '#61dafb',
  separator: '#2a2d33',

  success: '#28a745',
  successText: '#51cf66',
  successBg: 'rgba(40, 167, 69, 0.15)',
  error: '#dc3545',
  errorText: '#ff6b6b',
  errorBg: 'rgba(220, 53, 69, 0.15)',
  warning: '#ffc107',
  warningText: '#ffd700',
  info: '#0dcaf0',

  pickCorrect: '#28a745',
  pickIncorrect: '#dc3545',
  spreadLine: '#ffc107',

  shadowColor: '#000',
};

const Colors = {
  brand,
  light,
  dark,
} as const;

export default Colors;
export { Colors };

/** Resolve a theme palette from a color scheme string. Null/undefined → light. */
export function getTheme(scheme: string | null | undefined): typeof Colors['light'] {
  return scheme === 'dark' ? Colors.dark : Colors.light;
}
