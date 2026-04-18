import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
import autoprefixer from "autoprefixer";
import tailwindcss from "tailwindcss";
import prefixer from "postcss-prefix-selector";

const outDir = "../../backend/dCMS.Web/App_Plugins/DcmsV16/dist";

export default defineConfig({
  plugins: [react()],
  css: {
    postcss: {
      plugins: [
        tailwindcss(),
        autoprefixer(),
        prefixer({
          prefix: ".dcmsReportsSpa",
          exclude: [/^\.dcmsReportsSpa/, /:root/, /^@/],
          transform(prefix, selector, prefixedSelector) {
            if (/^(html|body|:host)(\s|$|::?)/.test(selector)) {
              return selector.replace(/^(html|body|:host)/, prefix);
            }
            return prefixedSelector;
          },
        }),
      ],
    },
  },
  build: {
    outDir,
    emptyOutDir: false,
    lib: {
      entry: "src/reports/index.tsx",
      formats: ["es"],
      fileName: () => "reports-spa.js",
    },
    cssCodeSplit: false,
    sourcemap: true,
    rollupOptions: {
      output: {
        banner:
          '(()=>{try{const g=globalThis;if(!g.process)g.process={env:{}};if(!g.process.env)g.process.env={};if(!g.process.env.NODE_ENV)g.process.env.NODE_ENV="production";}catch{}})();',
        assetFileNames: (assetInfo) => {
          if (assetInfo.name && assetInfo.name.endsWith(".css")) return "reports-spa.css";
          return "reports-spa.[ext]";
        },
      },
    },
  },
});
