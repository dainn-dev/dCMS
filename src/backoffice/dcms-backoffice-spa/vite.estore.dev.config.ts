/**
 * Vite dev server config for eStore SPA local development.
 * Proxies /gateway/** → dCMS.Gateway on localhost:5100.
 *
 * Usage: vite --config vite.estore.dev.config.ts
 */
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      // All gateway routes → dCMS.Gateway (YARP)
      "/gateway": {
        target: "http://localhost:5100",
        changeOrigin: true,
        secure: false,
      },
      // Umbraco Management API (languages, etc.)
      "/umbraco": {
        target: "http://localhost:5000",
        changeOrigin: true,
        secure: false,
      },
    },
  },
  build: {
    // Dev builds not used for Umbraco — only for local preview
    outDir: "dist-dev",
  },
});
