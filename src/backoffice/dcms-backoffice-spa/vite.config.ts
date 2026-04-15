import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// Build output goes directly into Umbraco App_Plugins so the backoffice can load it.
const outDir = "../../backend/dCMS.Web/App_Plugins/DcmsV16/dist";

export default defineConfig({
  plugins: [react()],
  build: {
    outDir,
    emptyOutDir: false,
    lib: {
      entry: "src/index.tsx",
      formats: ["es"],
      fileName: () => "orders-spa.js",
    },
    cssCodeSplit: false,
    sourcemap: true,
    rollupOptions: {
      output: {
        banner:
          '(()=>{try{const g=globalThis;if(!g.process)g.process={env:{}};if(!g.process.env)g.process.env={};if(!g.process.env.NODE_ENV)g.process.env.NODE_ENV="production";}catch{}})();',
        assetFileNames: (assetInfo) => {
          if (assetInfo.name && assetInfo.name.endsWith(".css")) return "orders-spa.css";
          return "orders-spa.[ext]";
        },
      },
    },
  },
});

