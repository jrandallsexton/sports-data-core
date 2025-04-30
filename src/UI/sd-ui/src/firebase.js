// src/firebase.js
import { initializeApp } from "firebase/app";
import { getAuth } from "firebase/auth";

const firebaseConfig = {
  apiKey: "AIzaSyBRkQwtEl3jeqoYKBN-hPv8VxTjUycNJgM",
  authDomain: "sportdeets-dev.firebaseapp.com",
};

const firebaseApp = initializeApp(firebaseConfig);
const auth = getAuth(firebaseApp);

export { auth };
