/**
 * Sentry initialization — runs once at app boot.
 *
 * This module is imported for its side effects from `app/_layout.tsx` as
 * the very first import line so Sentry is wired before any other module
 * (Firebase, React Query, navigation) executes. Earlier this week the
 * production app crashed on launch because Firebase init threw against
 * undefined config — that exact class of early-boot failure is what
 * Sentry's native crash reporting catches when it loads first.
 *
 * The DSN comes from `EXPO_PUBLIC_SENTRY_DSN`:
 *   - Dev:   `.env.local`
 *   - Prod:  `eas.json` `preview` / `production` `env` blocks
 *
 * If the DSN is unset (e.g. local-only branch, contributor without
 * Sentry access), the SDK no-ops gracefully and errors still bubble to
 * `ErrorBoundary` — they just don't get reported. A warning fires in
 * production builds so a missing DSN is loud.
 */
import * as Sentry from '@sentry/react-native';

const dsn = process.env.EXPO_PUBLIC_SENTRY_DSN;

// Environment tag — drives filtering / alerting / release tracking in the
// Sentry UI. Three distinct buckets so TestFlight (preview) crashes from
// internal testers don't blend with real App Store (production) users:
//   - dev:        Metro running locally
//   - preview:    EAS preview profile (internal TestFlight builds)
//   - production: EAS production profile (App Store releases)
// EXPO_PUBLIC_SENTRY_ENV is set per profile in eas.json; the fallback
// to 'production' is defensive in case a new profile forgets to set it.
const environment = __DEV__
  ? 'development'
  : process.env.EXPO_PUBLIC_SENTRY_ENV || 'production';

if (dsn) {
  Sentry.init({
    dsn,
    environment,
    // Perf tracing off for now — flip to a small sample (e.g. 0.1) when
    // we want span data on slow screen transitions. Keeps event volume
    // proportional to actual errors during early use.
    tracesSampleRate: 0,
  });
} else if (!__DEV__) {
  // Prod build without a DSN is a misconfiguration. Don't throw — errors
  // still bubble to ErrorBoundary, just nothing gets reported.
  console.warn(
    '[Sentry] EXPO_PUBLIC_SENTRY_DSN is unset in a production build; crash reporting is disabled.'
  );
}
