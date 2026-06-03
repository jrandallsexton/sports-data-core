// src/firebase.js
import { initializeApp } from "firebase/app";
import { getAuth, setPersistence, browserLocalPersistence } from "firebase/auth";

const firebaseConfig = {
  apiKey: "AIzaSyD2z-aIlO1REuGmdiw1Z2kmcUgrpDl4-ko",
  authDomain: "sportdeets.firebaseapp.com",
  projectId: "sportdeets",
  storageBucket: "sportdeets.firebasestorage.app",
  messagingSenderId: "812654295319",
  appId: "1:812654295319:web:bb9e42d84312b00c9a1f52",
};

const firebaseApp = initializeApp(firebaseConfig);
const auth = getAuth(firebaseApp);

// browserLocalPersistence is backed by window.localStorage (per Firebase
// Auth docs/source); the LOCAL label means auth state survives page
// refreshes and tab suspensions, and the storage event broadcasts state
// changes across tabs of the same origin so all tabs share one session.
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
// auth.signOut() broadcast the sign-out to every tab via the
// localStorage storage event.

export { auth };
