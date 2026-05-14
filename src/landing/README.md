# Relego Landing Page

Static Astro app for feature 008. This project is intentionally separate from the .NET solution.

## Structure

```text
/
├── astro.config.mjs
├── config/
├── layouts/
├── pages/
├── public/
├── styles/
├── tests/
└── package.json
```

`astro.config.mjs` sets `srcDir: '.'`, so Astro reads routes from `pages/` at the project root.

## Commands

```sh
npm install
npm run dev
npm run build
```

The local dev server runs at `http://localhost:4321/sunny-sunday/`.
