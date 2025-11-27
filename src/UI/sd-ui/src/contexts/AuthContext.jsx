import { createContext, useContext, useEffect, useState } from 'react';
import { getAuth, onAuthStateChanged, signOut as firebaseSignOut } from 'firebase/auth';
import apiClient from '../api/apiClient';

const AuthContext = createContext();

export function AuthProvider({ children }) {
  const [user, setUser] = useState(null);
  const [loading, setLoading] = useState(true);

  const handleSignOut = async () => {
    const auth = getAuth();
    try {
      console.log('Signing out: Clearing token cookie');
      // Clear the token cookie first
      await apiClient.post('/auth/clear-token');
      
      // Then sign out from Firebase and clear persisted auth state
      await firebaseSignOut(auth);
      await auth.signOut(); // This clears the persisted auth state
      
      setUser(null);
    } catch (error) {
      console.error('Sign out failed:', error);
      throw error;
    }
  };

  const setToken = async (firebaseUser, forceRefresh = false) => {
    try {
      const token = await firebaseUser.getIdToken(forceRefresh);
      //console.log('Setting token cookie for user:', firebaseUser.uid);
      await apiClient.post('/auth/set-token', { token });
      setUser(firebaseUser);
    } catch (error) {
      console.error('Token setup failed:', error);
      // Don't clear user on token refresh failures
      if (!forceRefresh) {
        setUser(null);
      }
    }
  };

  useEffect(() => {
    const auth = getAuth();
    
    const unsubscribe = onAuthStateChanged(auth, async (firebaseUser) => {
      //console.log('Auth state changed:', firebaseUser ? 'User present' : 'No user');
      
      if (firebaseUser) {
        // Only set the token if we're explicitly signing in
        // This prevents automatic token setting on page refresh
        if (user === null) {
          await setToken(firebaseUser);
        } else {
          setUser(firebaseUser);
        }
      } else {
        // Only clear the token if we're explicitly signing out
        if (user !== null) {
          //console.log('Clearing token cookie due to auth state change');
          await apiClient.post('/auth/clear-token');
        }
        setUser(null);
      }

      setLoading(false);
    });

    return () => {
      unsubscribe();
    };
  }, [user]);

  // Refresh token every 50 minutes (tokens expire after 1 hour)
  useEffect(() => {
    if (!user) return;

    const refreshInterval = setInterval(async () => {
      console.log('Refreshing Firebase token...');
      await setToken(user, true);
    }, 50 * 60 * 1000); // 50 minutes

    return () => clearInterval(refreshInterval);
  }, [user]);

  return (
    <AuthContext.Provider value={{ user, loading, handleSignOut, setToken }}>
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
