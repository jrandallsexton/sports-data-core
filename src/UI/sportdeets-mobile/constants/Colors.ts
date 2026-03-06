// ─── Brand palette ────────────────────────────────────────────────────────────
export const brand = {
  navy: '#1B3A6B',
  navyLight: '#2A5298',
  gold: '#D4AF37',
  goldLight: '#F0D060',
};

// ─── Semantic tokens per color scheme ─────────────────────────────────────────
const Colors = {
  brand,
  light: {
    text: '#1A1A2E',
    textMuted: '#6B7280',
    background: '#F0F2F5',
    card: '#FFFFFF',
    tint: brand.navy,
    tabIconDefault: '#9CA3AF',
    tabIconSelected: brand.navy,
    border: '#E5E7EB',
    separator: '#F3F4F6',
    success: '#16A34A',
    error: '#DC2626',
    warning: '#D97706',
  },
  dark: {
    text: '#F1F5F9',
    textMuted: '#94A3B8',
    background: '#0F172A',
    card: '#1E293B',
    tint: brand.gold,
    tabIconDefault: '#475569',
    tabIconSelected: brand.gold,
    border: '#334155',
    separator: '#1E293B',
    success: '#22C55E',
    error: '#F87171',
    warning: '#FBBF24',
  },
};

export default Colors;
export { Colors };

/** Type-safe theme accessor — handles null/undefined from useColorScheme */
export function getTheme(scheme: string | null | undefined): typeof Colors['light'] {
  return scheme === 'dark' ? Colors.dark : Colors.light;
}
