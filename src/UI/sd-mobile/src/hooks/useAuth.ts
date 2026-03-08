import { useEffect } from 'react';
import { onAuthStateChanged } from 'firebase/auth';
import { auth } from '@/src/lib/firebase';
import { useAuthStore } from '@/src/stores/authStore';

/**
 * Sets up the Firebase `onAuthStateChanged` listener and syncs state into Zustand.
 * Call exactly once from the root `_layout.tsx`.
 */
export function useAuthInit(): void {
  const { setUser, setInitialized } = useAuthStore();

  useEffect(() => {
    const unsubscribe = onAuthStateChanged(auth, (user) => {
      setUser(user);
      setInitialized(true);
    });
    return unsubscribe;
  }, []);
}

/** Returns the current auth state from the Zustand store. */
export function useAuth() {
  const { user, isInitialized, isLoading } = useAuthStore();
  return {
    user,
    isInitialized,
    isLoading,
    isAuthenticated: user !== null,
  };
}
