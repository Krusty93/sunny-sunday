# Quick Start: Landing Page

**Feature**: 008-landing-page
**Date**: 2026-05-14

---

## Prerequisites

- Node.js 24 installed (`node --version`)
- npm 9+ installed (`npm --version`)

The landing page is **completely independent** from the .NET solution. You do not need the .NET SDK to work on it.

## Project Location

```
src/landing/
├── astro.config.mjs       # Astro configuration
├── package.json            # Node.js dependencies
├── tsconfig.json           # TypeScript configuration
├── public/                 # Static assets (favicon, etc.)
├── assets/                 # Images (hero-kindle.jpg)
├── components/             # Astro components
│   ├── Accordion.astro
│   ├── Button.astro
│   ├── FeatureCard.astro
│   ├── Footer.astro
│   ├── GettingStarted.astro
│   ├── Navbar.astro
│   ├── Section.astro
│   └── ThemeToggle.astro
├── config/
│   └── site.ts             # Site configuration (GitHub URL, etc.)
├── layouts/
│   └── Layout.astro        # Base HTML layout
├── pages/
│   └── index.astro         # The landing page
├── styles/
│   └── global.css          # Global styles + Tailwind
└── tests/                  # Playwright E2E tests
    ├── navigation.spec.ts
    ├── theme.spec.ts
    ├── content-sections.spec.ts
    ├── faq.spec.ts
    └── accessibility.spec.ts
```

## Install & Run

```bash
# Navigate to the landing page project
cd src/landing

# Install dependencies
npm install

# Start dev server (http://localhost:4321)
npm run dev
```

## Build for Production

```bash
cd src/landing

# Build static site
npm run build

# Preview the production build locally
npm run preview
```

The built site is output to `src/landing/dist/`.

## Run E2E Tests

```bash
cd src/landing

# Install Playwright browsers (first time only)
npx playwright install --with-deps chromium

# Run all tests
npx playwright test

# Run a specific test file
npx playwright test tests/faq.spec.ts

# Run tests with UI mode
npx playwright test --ui
```

Tests run against the Astro dev server, which Playwright starts automatically via the `webServer` config in `playwright.config.ts`.

## Key Configuration

### Site Config (`config/site.ts`)

Update the GitHub URL and other site-wide settings here:

```typescript
export const siteConfig = {
  name: "Relego",
  logotype: "relego.",
  tagline: "Periodic highlights recap, delivered to your Kindle. For free.",
  githubUrl: "https://github.com/Krusty93/relego",
  docsUrl: "/docs",
  license: "MIT",
} as const;
```

### Astro Config (`astro.config.mjs`)

The `base` and `site` settings control the GitHub Pages deployment path:

```javascript
export default defineConfig({
  site: 'https://krusty93.github.io',
  base: '/sunny-sunday/',
  // ...
});
```

## Deployment

The landing page deploys to GitHub Pages via a dedicated CI workflow (`.github/workflows/landing-page.yml`). It triggers on pushes to `main` that modify files under `src/landing/`.
