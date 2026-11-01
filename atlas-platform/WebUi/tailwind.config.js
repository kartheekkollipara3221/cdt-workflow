// LOCAL ONLY. The target tailwind.config.js, with the theme from the target look (local/make-target-look.mjs).
// Tailwind output is made only where a stylesheet has @tailwind rules: target-look/styles.scss has them, the
// default local styles.scss does not, so the default build does not change.
const fs = require('fs');
const path = require('path');
const theming = (name) => {
  const file = path.join(__dirname, 'target-look', 'theming', `${name}.js`);
  return fs.existsSync(file) ? require(file) : {};
};

module.exports = {
  content: ['./src/**/*.{html,ts}'],
  // The Tailwind classes that platform app JSON uses (TTDV2.json Class/DivClass); INTEGRATION.md section 2 (review FABLE O18).
  safelist: ['grid', 'grid-cols-2', 'grid-cols-3', 'gap-6', 'items-start', 'p-8', 'bg-white'],
  theme: {
    extend: {
      fontFamily: theming('fonts'),
      colors: theming('colors'),
    },
  },
  plugins: [],
};
