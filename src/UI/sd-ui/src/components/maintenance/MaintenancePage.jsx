import React from "react";

export default function MaintenancePage() {
  return (
    <div style={{
      background: "#23272f",
      color: "#f8f9fa",
      fontFamily: "'Segoe UI', Arial, sans-serif",
      display: "flex",
      flexDirection: "column",
      alignItems: "center",
      justifyContent: "center",
      minHeight: "100vh",
      margin: 0
    }}>
      <div style={{
        background: "#2a2d33",
        borderRadius: 12,
        boxShadow: "0 4px 24px rgba(0,0,0,0.18)",
        padding: "2rem 2.5rem",
        textAlign: "center",
        maxWidth: 400
      }}>
        <div style={{ fontSize: "3rem", marginBottom: "1rem" }}>&#128295;</div>
        <h1 style={{ fontSize: "2rem", marginBottom: "1rem" }}>Site Down for Maintenance</h1>
        <p style={{ fontSize: "1.1rem", marginBottom: "1.5rem" }}>
          We're performing scheduled maintenance.<br />
          Please check back soon!
        </p>
        <p style={{ fontSize: "0.95rem", color: "#b0b3b8" }}>
          If you need assistance, contact <a href="mailto:support@sportdeets.com" style={{ color: "#f8f9fa", textDecoration: "underline" }}>support@sportdeets.com</a>
        </p>
      </div>
    </div>
  );
}
