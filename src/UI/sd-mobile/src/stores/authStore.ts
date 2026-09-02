import { create } from 'zustand';
import type { User } from 'firebase/auth';

interface AuthState {
  /** Firebase user; null when signed out */
  user: User | null;
  /** True while a sign-in/out operation is in flight */
  isLoading: boolean;
  /** True once onAuthStateChanged has fired for the first time */
  isInitialized: boolean;
  /**
   * Holds AuthGuard's redirect into the app while sign-up finishes its
   * post-create work (persisting the typed display name through the
   * validated PATCH, which can reject — e.g. the profanity filter — and
   * needs the sign-up form still on screen to show the error inline).
   * Without this, the guard navigates the moment Firebase signs the new
   * user in, racing that work.
   */
  signupHold: boolean;

  setUser: (user: User | null) => void;
  setLoading: (loading: boolean) => void;
  setInitialized: (initialized: boolean) => void;
  setSignupHold: (hold: boolean) => void;
}

export const useAuthStore = create<AuthState>((set) => ({
  user: null,
  isLoading: false,
  isInitialized: false,
  signupHold: false,

  setUser: (user) => set({ user }),
  setLoading: (isLoading) => set({ isLoading }),
  setInitialized: (isInitialized) => set({ isInitialized }),
  setSignupHold: (signupHold) => set({ signupHold }),
}));
