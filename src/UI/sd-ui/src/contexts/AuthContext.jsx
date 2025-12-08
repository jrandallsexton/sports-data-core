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

  // Refresh token every 50 minutes (tokens expire after 1 hour)
  // Firebase handles this automatically, but we force it to ensure fresh tokens
  useEffect(() => {
    if (!user) return;

    const refreshInterval = setInterval(async () => {
      console.log('Forcing Firebase token refresh...');
      try {
        await user.getIdToken(true); // Force refresh
        console.log('Token refreshed successfully');
      } catch (error) {
        console.error('Token refresh failed:', error);
      }
    }, 50 * 60 * 1000); // 50 minutes

    return () => clearInterval(refreshInterval);
  }, [user]);

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
