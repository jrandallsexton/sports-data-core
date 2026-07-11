/**
 * Firebase throws `auth/account-exists-with-different-credential` when the
 * email is already registered under a different provider (e.g. the user signed
 * up with Google, then tries Apple with the same email). Both provider flows
 * surface a clear, actionable message instead of the raw code.
 */
export function isAccountExistsWithDifferentCredential(err: unknown): boolean {
  return (
    (err as { code?: string })?.code ===
    'auth/account-exists-with-different-credential'
  );
}

export class AccountExistsWithDifferentCredentialError extends Error {
  constructor() {
    super(
      'That email is already registered with a different sign-in method. '
        + 'Please sign in with that method instead.',
    );
    this.name = 'AccountExistsWithDifferentCredentialError';
  }
}
