import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";
import { fileURLToPath } from "node:url";

// The app build resolves bare imports from src via tsconfig.json
// ("baseUrl": "src"), e.g. "api/leagues/leaguesApi"; vitest ignores baseUrl.
// Mirror it for the bare prefixes the code actually uses, so components that
// import that way can be tested. Explicit list: a new bare prefix needs an
// entry here.
const srcDir = fileURLToPath(new URL("./src/", import.meta.url));
const srcBareImportAliases = ["api", "components", "hooks"].map((dir) => ({
  find: new RegExp(`^${dir}/`),
  replacement: `${srcDir}${dir}/`,
}));

// The app is on React 19 + react-router 7, which react-scripts@5 / jest 27 can't
// test (jest's resolver chokes on RRD7's exports map; jsdom's transitive
// http-proxy-agent breaks require() under modern Node). Vitest handles ESM /
// exports / React 19 natively on Node 20. `react()` also transforms JSX in .js
// files (CRA allows JSX there) via the include filter.
export default defineConfig({
  plugins: [react({ include: /src\/.*\.(js|jsx|ts|tsx)$/ })],
  resolve: { alias: srcBareImportAliases },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: "./src/setupTests.js",
    include: ["src/**/*.{test,spec}.{js,jsx,ts,tsx}"],
    css: false,
  },
});
