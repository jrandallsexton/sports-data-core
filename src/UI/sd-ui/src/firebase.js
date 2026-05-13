// src/firebase.js
import { initializeApp } from "firebase/app";
import { getAuth, setPersistence, browserLocalPersistence } from "firebase/auth";

const firebaseConfig = {
  apiKey: "AIzaSyBRkQwtEl3jeqoYKBN-hPv8VxTjUycNJgM",
  authDomain: "sportdeets-dev.firebaseapp.com",
};

const firebaseApp = initializeApp(firebaseConfig);
const auth = getAuth(firebaseApp);

// Persistence in IndexedDB so auth state survives refreshes and tab
// suspensions, and so all tabs of the same origin share one session.
setPersistence(auth, browserLocalPersistence)
  .then(() => {
    console.log('✅ Firebase persistence set to LOCAL');
  })
  .catch((error) => {
    console.error('❌ Failed to set Firebase persistence:', error);
    console.error('Auth sessions may not survive tab suspensions!');
  });

// No visibilitychange handler: token validity is enforced per-request
// by apiClient (proactive refresh if <5min to expiry, force-refresh +
// retry on 401). A handler that force-refreshed on every tab refocus
// caused spurious logouts — two tabs racing the same forced refresh
// (e.g. when opening a contest overview in a new tab and switching
// back) would occasionally fail one refresh, hit the catch path, and
// auth.signOut() broadcast the sign-out to every tab via IndexedDB.

export { auth };
