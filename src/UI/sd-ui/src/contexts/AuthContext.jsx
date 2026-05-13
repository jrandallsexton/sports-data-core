import { createContext, useContext, useEffect, useState } from 'react';
import { getAuth, onAuthStateChanged, signOut as firebaseSignOut } from 'firebase/auth';

const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

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
    
    const unsubscribe = onAuthStateChanged(auth, async (firebaseUser) => {
      console.log('Auth state changed:', firebaseUser ? `User: ${firebaseUser.uid}` : 'No user');
      
      setUser(firebaseUser);
      setLoading(false);
    });

    return () => {
      unsubscribe();
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
