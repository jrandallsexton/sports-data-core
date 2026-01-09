// src/firebase.js
import { initializeApp } from "firebase/app";
import { getAuth, setPersistence, browserLocalPersistence } from "firebase/auth";

const firebaseConfig = {
  apiKey: "AIzaSyBRkQwtEl3jeqoYKBN-hPv8VxTjUycNJgM",
  authDomain: "sportdeets-dev.firebaseapp.com",
};

const firebaseApp = initializeApp(firebaseConfig);
const auth = getAuth(firebaseApp);

// ✅ CRITICAL: Explicitly set persistence to LOCAL
// This ensures auth state survives browser refreshes and tab suspensions
setPersistence(auth, browserLocalPersistence)
  .then(() => {
    console.log('✅ Firebase persistence set to LOCAL');
  })
  .catch((error) => {
    console.error('❌ Failed to set Firebase persistence:', error);
    console.error('Auth sessions may not survive tab suspensions!');
  });

// ✅ Add session recovery on visibility change (tab resume from suspension)
if (typeof document !== 'undefined') {
  document.addEventListener('visibilitychange', async () => {
    if (!document.hidden && auth.currentUser) {
      console.log('📱 Tab became visible, verifying auth state...');
      try {
        // Verify token is still valid after tab was suspended/backgrounded
        await auth.currentUser.getIdToken(true);
        console.log('✅ Auth state verified after tab resume');
      } catch (error) {
        console.error('❌ Auth state invalid after tab resume:', error);
        console.warn('Session was lost during tab suspension - redirecting to login');
        // Session was lost during suspension - gracefully sign out and redirect
        await auth.signOut();
        if (window.location.pathname !== '/') {
          window.location.href = '/';
        }
      }
    }
  });
}

export { auth };
