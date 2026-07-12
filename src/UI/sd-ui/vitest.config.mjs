import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

// The app is on React 19 + react-router 7, which react-scripts@5 / jest 27 can't
// test (jest's resolver chokes on RRD7's exports map; jsdom's transitive
// http-proxy-agent breaks require() under modern Node). Vitest handles ESM /
// exports / React 19 natively on Node 20. `react()` also transforms JSX in .js
// files (CRA allows JSX there) via the include filter.
export default defineConfig({
  plugins: [react({ include: /src\/.*\.(js|jsx|ts|tsx)$/ })],
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/setupTests.js",
    include: ["src/**/*.{test,spec}.{js,jsx,ts,tsx}"],
    css: false,
  },
});
