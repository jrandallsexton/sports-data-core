import {
  DEFAULT_API_BASE_URL,
  InvalidReleaseConfigError,
  isNonPublicHost,
  resolveApiBaseUrl,
  resolveSignalRUrl,
  sentryEnvWarning,
} from '@/src/lib/releaseConfig';

describe('isNonPublicHost', () => {
  it.each([
    'http://localhost:5262',
    'http://LOCALHOST',
    'http://app.localhost',
    'http://127.0.0.1:5262',
    'http://127.5.5.5',
    'http://[::1]:5262',
    'http://0.0.0.0',
    'http://10.0.0.4:5262',
    'http://192.168.1.20:5262',
    'http://172.16.0.1',
    'http://172.31.255.255',
    'http://169.254.10.10',
    'http://bender.local:5262',
    'http://[fe80::1]:5262', // IPv6 link-local
    'http://[febf::1]', // top of fe80::/10
    'http://[fc00::1]', // unique local, fc00::/7
    'http://[fd12:3456::1]:5262',
    'http://[::ffff:192.168.1.20]:5262', // IPv4-mapped private, dotted input
    'http://[::ffff:c0a8:114]', // the same address as the parser normalises it
    'http://[::ffff:127.0.0.1]',
    'http://[::ffff:7f00:1]',
  ])('flags %s', (url) => {
    expect(isNonPublicHost(url)).toBe(true);
  });

  it.each([
    'https://api.sportdeets.com',
    'https://api.sportdeets.com/',
    'http://api.sportdeets.com:8080',
    'https://172.15.0.1', // just outside 172.16/12
    'https://172.32.0.1',
    'https://192.169.0.1',
    'https://11.0.0.1',
    'https://8.8.8.8',
    'https://[2606:4700::1111]', // public IPv6
    'https://[fec0::1]', // just past fe80::/10
    'https://[fe00::1]', // just below fe80::/10
    'https://[::ffff:8.8.8.8]', // IPv4-mapped public
  ])('allows %s', (url) => {
    expect(isNonPublicHost(url)).toBe(false);
  });

  it('does not flag an unparseable value', () => {
    expect(isNonPublicHost('not a url')).toBe(false);
    expect(isNonPublicHost('')).toBe(false);
    // A zone id is not valid in a URL host per WHATWG; the parser rejects it
    // outright, on device as in Node, so the request layer fails on it.
    expect(isNonPublicHost('http://[fe80::1%25en0]')).toBe(false);
  });
});

describe('resolveApiBaseUrl', () => {
  it('falls back to the production default when unset or blank', () => {
    expect(resolveApiBaseUrl(undefined, false)).toBe(DEFAULT_API_BASE_URL);
    expect(resolveApiBaseUrl('', false)).toBe(DEFAULT_API_BASE_URL);
    expect(resolveApiBaseUrl('   ', false)).toBe(DEFAULT_API_BASE_URL);
  });

  it('returns a public URL untouched in a release bundle', () => {
    expect(resolveApiBaseUrl('https://api.sportdeets.com', false)).toBe(
      'https://api.sportdeets.com'
    );
  });

  it('throws on the exact value that dark-screened iOS 1.2.1', () => {
    expect(() => resolveApiBaseUrl('http://localhost:5262', false)).toThrow(
      InvalidReleaseConfigError
    );
    expect(() => resolveApiBaseUrl('http://localhost:5262', false)).toThrow(
      /localhost:5262/
    );
  });

  it('throws on a LAN address in a release bundle', () => {
    expect(() => resolveApiBaseUrl('http://192.168.1.20:5262', false)).toThrow(
      InvalidReleaseConfigError
    );
  });

  it('permits localhost and LAN in a dev bundle', () => {
    expect(resolveApiBaseUrl('http://localhost:5262', true)).toBe('http://localhost:5262');
    expect(resolveApiBaseUrl('http://192.168.1.20:5262', true)).toBe(
      'http://192.168.1.20:5262'
    );
  });

  it('names the error so it is recognisable in Sentry', () => {
    let caught: unknown;
    try {
      resolveApiBaseUrl('http://localhost:5262', false);
    } catch (e) {
      caught = e;
    }
    expect(caught).toBeInstanceOf(InvalidReleaseConfigError);
    expect((caught as Error).name).toBe('InvalidReleaseConfigError');
  });
});

describe('resolveSignalRUrl', () => {
  const api = 'https://api.sportdeets.com';

  it('falls back to the API base when unset', () => {
    expect(resolveSignalRUrl(undefined, api, false)).toBe(api);
    expect(resolveSignalRUrl('', api, false)).toBe(api);
  });

  it('honours a public override', () => {
    expect(resolveSignalRUrl('https://hub.sportdeets.com', api, false)).toBe(
      'https://hub.sportdeets.com'
    );
  });

  it('throws on a non-public override in a release bundle', () => {
    expect(() => resolveSignalRUrl('http://localhost:5262', api, false)).toThrow(
      InvalidReleaseConfigError
    );
  });

  it('permits a non-public override in dev', () => {
    expect(resolveSignalRUrl('http://localhost:5262', api, true)).toBe(
      'http://localhost:5262'
    );
  });
});

describe('sentryEnvWarning', () => {
  it('warns when unset in a release bundle', () => {
    expect(sentryEnvWarning(undefined, false)).toMatch(/EXPO_PUBLIC_SENTRY_ENV is unset/);
    expect(sentryEnvWarning('', false)).toMatch(/unset/);
  });

  it('is silent when set, or in dev', () => {
    expect(sentryEnvWarning('production', false)).toBeNull();
    expect(sentryEnvWarning(undefined, true)).toBeNull();
  });
});

describe('boot-time evaluation', () => {
  const g = globalThis as unknown as { __DEV__: boolean };
  const originalDev = g.__DEV__;
  const originalApi = process.env.EXPO_PUBLIC_API_BASE_URL;
  const originalSentryEnv = process.env.EXPO_PUBLIC_SENTRY_ENV;

  afterEach(() => {
    g.__DEV__ = originalDev;
    process.env.EXPO_PUBLIC_API_BASE_URL = originalApi;
    process.env.EXPO_PUBLIC_SENTRY_ENV = originalSentryEnv;
    jest.restoreAllMocks();
  });

  function loadModule() {
    let mod: typeof import('@/src/lib/releaseConfig') | undefined;
    jest.isolateModules(() => {
      mod = require('@/src/lib/releaseConfig');
    });
    return mod!;
  }

  it('exports resolved values in a dev bundle (jest-expo sets __DEV__=true)', () => {
    const mod = loadModule();
    expect(typeof mod.API_BASE_URL).toBe('string');
    expect(typeof mod.SIGNALR_URL).toBe('string');
  });

  it('throws on import when a release bundle carries the .env.local value', () => {
    // The 2026-09-17 outage, reproduced: __DEV__ false, value inlined from
    // .env.local. Importing the module is the boot-time assertion.
    g.__DEV__ = false;
    process.env.EXPO_PUBLIC_API_BASE_URL = 'http://localhost:5262';
    process.env.EXPO_PUBLIC_SENTRY_ENV = 'production';
    // isolateModules gives the module its own copy of the error class, so
    // match on name and message rather than constructor identity.
    let caught: unknown;
    try {
      loadModule();
    } catch (e) {
      caught = e;
    }
    expect(caught).toBeInstanceOf(Error);
    expect((caught as Error).name).toBe('InvalidReleaseConfigError');
    expect((caught as Error).message).toMatch(/localhost:5262/);
  });

  it('boots silently in a release bundle with production values', () => {
    g.__DEV__ = false;
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.sportdeets.com';
    process.env.EXPO_PUBLIC_SENTRY_ENV = 'production';
    const warn = jest.spyOn(console, 'warn').mockImplementation(() => {});
    const mod = loadModule();
    expect(mod.API_BASE_URL).toBe('https://api.sportdeets.com');
    expect(warn).not.toHaveBeenCalled();
  });

  it('warns, but boots, when a release bundle lacks the Sentry env tag', () => {
    g.__DEV__ = false;
    process.env.EXPO_PUBLIC_API_BASE_URL = 'https://api.sportdeets.com';
    delete process.env.EXPO_PUBLIC_SENTRY_ENV;
    const warn = jest.spyOn(console, 'warn').mockImplementation(() => {});
    loadModule();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining('EXPO_PUBLIC_SENTRY_ENV is unset'));
  });
});
