import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// Standalone Vite app (migrated off CRA 2026-09 — react-scripts is
// unmaintained and the old package.json was hand-pinning transitive CVEs).
// Served as static files by nginx in the cluster; no runtime env needed.
// VITE_VERSION lands in the footer at build time (Dockerfile passes it).
export default defineConfig({
  plugins: [react()],
  build: {
    // The Dockerfile copies this into the nginx image; named `build` to
    // match the sd-ui convention rather than Vite's default `dist`.
    outDir: 'build',
  },
});
