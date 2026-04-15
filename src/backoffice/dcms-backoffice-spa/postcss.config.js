import prefixer from "postcss-prefix-selector";

export default {
  plugins: {
    tailwindcss: {},
    autoprefixer: {},
    "postcss-prefix-selector": {
      prefix: ".dcmsOrdersSpa",
      // Don't prefix keyframes, @font-face, or the root itself.
      exclude: [/^\.dcmsOrdersSpa/, /:root/, /^@/],
      transform(prefix, selector, prefixedSelector) {
        // html / :host / body → scope into our container instead of resetting the whole page.
        if (/^(html|body|:host)(\s|$|::?)/.test(selector)) {
          return selector.replace(/^(html|body|:host)/, prefix);
        }
        return prefixedSelector;
      },
    },
  },
};
