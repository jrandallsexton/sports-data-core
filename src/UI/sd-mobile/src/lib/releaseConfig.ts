/**
 * Release configuration guard — runs once at app boot.
 *
 * `EXPO_PUBLIC_*` values are inlined into the JS bundle by Metro at bundle
 * time. `eas update` bundles on the developer's machine, where Metro reads
 * `.env.local`, so an OTA bundle can carry a *defined but wrong* value such
 * as `EXPO_PUBLIC_API_BASE_URL=http://localhost:5262`. That is exactly what
 * dark-screened iOS 1.2.1 on 2026-09-17: every request went to the phone
 * itself, failed as a handled network error, and nothing anywhere treated
 * the configuration as invalid. A `?? fallback` cannot catch this class of
 * bug, because the value is set. Only an assertion can.
 *
 * This module is the single place the API base URL is resolved. It throws
 * `InvalidReleaseConfigError` in a release bundle (`!__DEV__`) when the
 * host is loopback or a private LAN address, so a misconfigured bundle
 * crashes loudly at boot — on a canary device, and in Sentry — instead of
 * failing silently in production.
 *
 * Imported for its side effects from `app/_layout.tsx` directly after
 * `src/lib/sentry.ts`, so a thrown error is reported. Also imported by the
 * modules that need the resolved URL.
 *
 * See docs/mobile/ota-pipeline-hardening.md, section 3.3.
 */

export const DEFAULT_API_BASE_URL = 'https://api.sportdeets.com';

export class InvalidReleaseConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = 'InvalidReleaseConfigError';
  }
}

/**
 * True when the URL points at the device itself or a private network —
 * reachable from a developer machine, never from a user's phone.
 */
export function isNonPublicHost(url: string): boolean {
  let host: string;
  try {
    host = new URL(url).hostname.toLowerCase();
  } catch {
    // Unparseable is not "non-public"; let the request layer fail on it.
    return false;
  }
  // URL keeps brackets on IPv6 hosts.
  host = host.replace(/^\[|\]$/g, '');

  if (host === 'localhost' || host.endsWith('.localhost')) return true;
  if (host === '::1' || host === '0.0.0.0' || host === '::') return true;
  if (host.endsWith('.local')) return true;

  const octets = host.split('.');
  if (octets.length === 4 && octets.every((o) => /^\d{1,3}$/.test(o))) {
    const [a, b] = octets.map(Number);
    if (a === 127) return true; // loopback
    if (a === 10) return true; // 10/8
    if (a === 192 && b === 168) return true; // 192.168/16
    if (a === 172 && b >= 16 && b <= 31) return true; // 172.16/12
    if (a === 169 && b === 254) return true; // link-local
  }
  return false;
}

/**
 * Resolve the API base URL for this bundle.
 *
 * - unset → the production default (a store build without env is still a
 *   working app)
 * - set to a public host → returned as-is
 * - set to a loopback / private host in a release bundle → throws
 */
export function resolveApiBaseUrl(raw: string | undefined, isDev: boolean): string {
  const url = raw?.trim() ? raw.trim() : DEFAULT_API_BASE_URL;
  if (!isDev && isNonPublicHost(url)) {
    throw new InvalidReleaseConfigError(
      `EXPO_PUBLIC_API_BASE_URL resolves to a non-public host (${url}) in a release bundle. ` +
        'The bundle was almost certainly built with .env.local loaded. ' +
        'See docs/mobile/ota-pipeline-hardening.md.'
    );
  }
  return url;
}

/**
 * Resolve the SignalR hub base. Falls back to the (already validated) API
 * base, but an explicit override is subject to the same guard.
 */
export function resolveSignalRUrl(
  raw: string | undefined,
  apiBaseUrl: string,
  isDev: boolean
): string {
  const url = raw?.trim() ? raw.trim() : apiBaseUrl;
  if (!isDev && isNonPublicHost(url)) {
    throw new InvalidReleaseConfigError(
      `EXPO_PUBLIC_SIGNALR_URL resolves to a non-public host (${url}) in a release bundle.`
    );
  }
  return url;
}

/**
 * Warn when a release bundle carries no Sentry environment tag. Not fatal:
 * `src/lib/sentry.ts` falls back to `production`, but that makes OTA bundles
 * indistinguishable from store builds in Sentry. Returns the warning so the
 * caller (and tests) can see it.
 */
export function sentryEnvWarning(raw: string | undefined, isDev: boolean): string | null {
  if (isDev || raw?.trim()) return null;
  return (
    '[releaseConfig] EXPO_PUBLIC_SENTRY_ENV is unset in a release bundle; ' +
    "Sentry will tag events as 'production'. Publish with --environment so the " +
    'server-side value is inlined.'
  );
}

// ---- Boot-time resolution. Evaluated once, on first import. ----

export const API_BASE_URL: string = resolveApiBaseUrl(
  process.env.EXPO_PUBLIC_API_BASE_URL,
  __DEV__
);

export const SIGNALR_URL: string = resolveSignalRUrl(
  process.env.EXPO_PUBLIC_SIGNALR_URL,
  API_BASE_URL,
  __DEV__
);

const warning = sentryEnvWarning(process.env.EXPO_PUBLIC_SENTRY_ENV, __DEV__);
if (warning) console.warn(warning);
