import { createContext, useContext, useEffect, useRef, useState } from 'react';
import { getAuth, onAuthStateChanged, onIdTokenChanged, signOut as firebaseSignOut } from 'firebase/auth';

const AuthContext = createContext();

// Diagnostic context for the spurious-logout investigation
// (docs/auth-spurious-logout-investigation.md). When onAuthStateChanged
// fires with null, we want to know: was the tab discarded and reloaded
// (browser memory pressure)? Was it just hidden? Was this a cross-tab
// broadcast from another tab's signOut()? The fields below distinguish
// those vectors so a single production occurrence is actionable.
function authDiagnosticContext() {
  const nav = typeof performance !== 'undefined'
    ? performance.getEntriesByType('navigation')[0]
    : null;
  return {
    visibilityState: typeof document !== 'undefined' ? document.visibilityState : 'unknown',
    // wasDiscarded: true means Chrome discarded this tab and we're a fresh
    // load on rehydration. Firebase briefly fires null during rehydration
    // before re-emitting the persisted user — that's not a real logout.
    wasDiscarded: typeof document !== 'undefined' ? document.wasDiscarded === true : false,
    navigationType: nav?.type ?? 'unknown',
    msSincePageLoad: typeof performance !== 'undefined' ? Math.round(performance.now()) : null,
  };
}

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);
  // Previous user.uid kept in a ref so the AuthState log can show
  // "had user X → now null" — a logout, vs "had null → still null" —
  // a redundant fire we shouldn't react to.
  const previousUidRef = useRef(null);

  const handleSignOut = async () => {
    const auth = getAuth();
    try {
      console.log('Signing out from Firebase');

      // Sign out from Firebase - this clears the persisted auth state
      await firebaseSignOut(auth);

      setUser(null);
    } catch (error) {
      console.error('Sign out failed:', error);
      throw error;
    }
  };

  useEffect(() => {
    const auth = getAuth();

    const unsubscribeAuth = onAuthStateChanged(auth, async (firebaseUser) => {
      const previousUid = previousUidRef.current;
      const nextUid = firebaseUser?.uid ?? null;
      const transitionedToNull = previousUid && !nextUid;

      console.log(
        'Auth state changed:',
        firebaseUser ? `User: ${firebaseUser.uid}` : 'No user',
        {
          previousUid,
          transitionedToNull,
          ...authDiagnosticContext(),
        }
      );

      previousUidRef.current = nextUid;
      setUser(firebaseUser);
      setLoading(false);
    });

    // onIdTokenChanged fires on initial sign-in, token refresh, and
    // sign-out. Logging it alongside onAuthStateChanged lets us see
    // refresh successes (token still valid) interleaved with the null
    // emissions — if a sign-out arrives without a preceding refresh
    // failure on THIS tab, that's strong evidence the signal came from
    // another tab via the cross-tab broadcast.
    const unsubscribeIdToken = onIdTokenChanged(auth, (firebaseUser) => {
      console.log(
        'ID token changed:',
        firebaseUser ? `User: ${firebaseUser.uid}` : 'No user',
        authDiagnosticContext()
      );
    });

    return () => {
      unsubscribeAuth();
      unsubscribeIdToken();
    };
  }, []);

  // Token refresh is owned by apiClient (request interceptor): expiration
  // is checked per-request and the token force-refreshes if <5min to
  // expiry, with a force-refresh + retry on 401. A previous experiment
  // also force-refreshed on every visibilitychange in firebase.js — that
  // raced when two tabs refocused at once and would auth.signOut() on a
  // single transient failure, which the localStorage storage event then
  // broadcast to every tab. The per-request path handles long
  // suspensions correctly the moment the user interacts.

  return (
    <AuthContext.Provider value={{ user, loading, handleSignOut }}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}
