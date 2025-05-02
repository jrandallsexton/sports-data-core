// src/components/Login.jsx
import React, { useState } from "react";
import { useNavigate } from "react-router-dom";
import { signInWithEmailAndPassword } from "firebase/auth";
import { auth } from "../../firebase"; // ✅ centralized Firebase setup
import { useAuth } from "../../contexts/AuthContext";
import { FaEnvelope, FaLock } from "react-icons/fa";
import "./Login.css";

const Login = () => {

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [errorMsg, setErrorMsg] = useState("");
  const navigate = useNavigate();
  const { setToken } = useAuth();

  const handleLogin = async (e) => {
    e.preventDefault();
    setErrorMsg("");

    try {
      const userCredential = await signInWithEmailAndPassword(auth, email, password);
      await setToken(userCredential.user);
      navigate("/app");
    } catch (error) {
      setErrorMsg(error.message || "Login failed");
    }
  };

  return (
    <div className="login-page">
      <div className="login-card">
        <h2>Sign In</h2>
        <form onSubmit={handleLogin}>
          <div className="form-group">
            <label>Email:</label>
            <div className="input-wrapper">
              <FaEnvelope />
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="you@example.com"
                required
              />
            </div>
          </div>

          <div className="form-group">
            <label>Password:</label>
            <div className="input-wrapper">
              <FaLock />
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="••••••••"
                required
              />
            </div>
          </div>

          {errorMsg && <div className="error-message">{errorMsg}</div>}
          <button type="submit" className="login-button">
            Sign In
          </button>
        </form>
      </div>
    </div>
  );
};

export default Login;
