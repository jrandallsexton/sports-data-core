import { create } from 'zustand';
import type { User } from 'firebase/auth';

interface AuthState {
  /** Firebase user; null when signed out */
  user: User | null;
  /** True while a sign-in/out operation is in flight */
  isLoading: boolean;
  /** True once onAuthStateChanged has fired for the first time */
  isInitialized: boolean;

  setUser: (user: User | null) => void;
  setLoading: (loading: boolean) => void;
  setInitialized: (initialized: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isLoading: false,
  isInitialized: false,

  setUser: (user) => set({ user }),
  setLoading: (isLoading) => set({ isLoading }),
  setInitialized: (isInitialized) => set({ isInitialized }),
}));
