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

  // NOTE: Interval-based token refresh removed - it doesn't work when tab is suspended.
  // Instead, we now:
  // 1. Check token expiration on each API request (apiClient.js request interceptor)
  // 2. Refresh proactively if token expires in <5 minutes
  // 3. Verify auth state when tab resumes from suspension (firebase.js visibility listener)
  // This is more reliable than a timer that doesn't run during tab suspension.

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
