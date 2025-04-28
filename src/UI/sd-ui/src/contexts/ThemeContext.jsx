// src/context/ThemeContext.js

import { createContext, useContext, useState, useEffect } from "react";

const ThemeContext = createContext();

export function ThemeProvider({ children }) {
  const [theme, setTheme] = useState(() => {
    // Try to load from localStorage first
    const storedTheme = localStorage.getItem("theme");
    return storedTheme || "dark"; // Default to dark if nothing saved
  });

  useEffect(() => {
    document.body.setAttribute("data-theme", theme);
    localStorage.setItem("theme", theme);
  }, [theme]);

  function toggleTheme() {
    setTheme((prev) => (prev === "dark" ? "light" : "dark"));
  }

  return (
    <ThemeContext.Provider value={{ theme, toggleTheme }}>
      {children}
    </ThemeContext.Provider>
  );
}

// Nice hook shortcut
export function useTheme() {
  return useContext(ThemeContext);
}
