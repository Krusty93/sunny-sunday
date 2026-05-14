# Research: Landing Page

**Feature**: 008-landing-page
**Phase**: 0 — Outline & Research
**Date**: 2026-05-14

---

## 1. Dockit Astro Template — Architecture & Conventions

### Decision: Use Dockit Astro as base, heavily customized for a single-page marketing site

**Rationale**: Dockit is built on Astro Starlight, which is a documentation framework. The landing page is a marketing page, not a documentation site. The template provides useful components (Accordion, Button, Grid, Section) and a working dark/light theme toggle. However, the Starlight-specific features (sidebar, search, multi-page docs, content collections for docs) are unnecessary. The approach is to scaffold a new Astro project at `src/landing/` that reuses Dockit's component patterns but strips out all documentation machinery.

**Alternatives considered**:
- Use Dockit as-is with Starlight's `index.mdx` — rejected because Starlight forces docs-specific layout (sidebar, breadcrumbs, table of contents), and the hero/sections customization would fight the framework
- Use plain Astro without Starlight — **chosen**. Cherry-pick Dockit's component patterns (Accordion, Section, Button, Grid) as standalone Astro components, without the Starlight integration
- Use a different Astro template (e.g., AstroWind) — rejected because user explicitly chose Dockit Astro as the starting point

### Key findings from template analysis

| Aspect | Dockit Template | Landing Page Approach |
|--------|----------------|----------------------|
| Framework | Astro + Starlight | Astro only (no Starlight) |
| Routing | Multi-page docs | Single `index.astro` page |
| Theme toggle | Starlight's built-in `ThemeSwitch.astro` | Custom `ThemeToggle.astro` (localStorage + `prefers-color-scheme`) |
| Accordion | `Accordion.astro` with `type` filter + `AccordionContainer.astro` | Simplified `Accordion.astro` (no tab categories needed) |
| CTA | `CTA.astro` using content collections | Inline in `index.astro` (no content collection needed) |
| Section | `Section.astro` with `title` + `description` + slot | Reuse pattern directly |
| Grid | `Grid.astro` with responsive columns | Reuse pattern directly |
| Config | `src/config/config.json`, `theme.json`, etc. | Single `src/config/site.ts` exporting typed config |
| Styling | Tailwind CSS via `@tailwindcss/vite` + global CSS | Tailwind CSS (same approach) |
| Package manager | Yarn | npm (consistent with .NET ecosystem convention; no lock-in reason for yarn) |

---

## 2. Astro Project Setup (Without Starlight)

### Decision: Scaffold a standalone Astro project with Tailwind CSS, no Starlight

**Rationale**: Since this is a single-page marketing site, Starlight adds unnecessary complexity (content collections for docs, sidebar config, i18n loaders). A plain Astro project with Tailwind CSS is simpler and gives full control over the layout.

**Setup steps**:
1. `npm create astro@latest -- --template minimal` inside `src/landing/`
2. Add `@astrojs/tailwind` or use `@tailwindcss/vite` plugin directly (Dockit uses the latter)
3. Create `src/pages/index.astro` as the single page
4. Create component files based on Dockit patterns

### Astro version

Use Astro 5.x (latest stable as of May 2026). Dockit uses Astro 5.x with Starlight 0.34+.

---

## 3. Dark/Light Theme Toggle

### Decision: Implement a standalone theme toggle using `localStorage` and CSS `data-theme` attribute

**Rationale**: Without Starlight, we don't have its built-in theme provider. The implementation follows a common Astro pattern:
1. An inline `<script>` in `<head>` reads `localStorage.getItem('theme')` or `prefers-color-scheme` and sets `data-theme` on `<html>` before paint (avoids flash)
2. A `ThemeToggle.astro` component provides a button that toggles the attribute and persists to `localStorage`
3. CSS uses `[data-theme='dark']` selectors (or Tailwind's `dark:` with `darkMode: 'selector'` targeting `[data-theme='dark']`)

**Alternatives considered**:
- Use `class="dark"` on `<html>` (Tailwind default) — acceptable but `data-theme` is more semantic and conventional in Astro templates
- Server-side theme detection — rejected because the page is fully static, deployed to GitHub Pages

### CSS variable for gradient overlay

The hero gradient uses `var(--color-bg)` which maps to the page background color. In light mode this is white/near-white; in dark mode it's the dark background. The gradient adapts automatically when the theme changes because CSS variables are re-evaluated.

---

## 4. GitHub Pages Deployment

### Decision: Deploy via GitHub Actions with the `actions/deploy-pages` workflow

**Rationale**: GitHub Pages is free, well-integrated with the repository, and requires no additional infrastructure. The Astro docs recommend a specific GitHub Actions workflow for deployment.

**Workflow details**:
- Trigger: push to `main` with changes in `src/landing/**`
- Build: `cd src/landing && npm ci && npm run build`
- Deploy: upload `dist/` artifact, deploy to Pages
- No custom domain needed initially (uses `krusty93.github.io/sunny-sunday/`)

**Base path**: Since this is a project page (not a user page), the site is served at `/sunny-sunday/`. The Astro config must set `base: '/sunny-sunday/'` and `site: 'https://krusty93.github.io'`.

**Alternatives considered**:
- Netlify / Vercel — rejected because GitHub Pages is simpler and the project already uses GitHub extensively
- Manual deploy — rejected because CI automation is explicitly required

---

## 5. Playwright E2E Testing

### Decision: Use Playwright with the Astro dev server for E2E tests

**Rationale**: Playwright is the user's chosen testing tool and is available via MCP in the dev environment. Tests run against the Astro dev server (`localhost:4321`) or a preview build.

**Test organization** (from user requirements):
- `tests/navigation.spec.ts` — Navbar links, smooth scroll CTA, Explore buttons
- `tests/theme.spec.ts` — Dark/light toggle, gradient adaptation, persistence
- `tests/content-sections.spec.ts` — All sections present and in correct order, step cards, feature cards
- `tests/faq.spec.ts` — Accordion expand/collapse
- `tests/accessibility.spec.ts` — axe-core audit via `@axe-core/playwright`, keyboard nav, alt text

**Configuration**: `playwright.config.ts` at `src/landing/` root with:
- `webServer` pointing to `npm run dev` (or `npm run preview` for CI)
- Base URL: `http://localhost:4321`
- Browsers: Chromium only (sufficient for static site testing)

---

## 6. Google Fonts Loading

### Decision: Load Playfair Display via Google Fonts `<link>` with `display=swap`

**Rationale**: The spec requires Playfair Display weight 300 for the logotype and headings. Google Fonts is the simplest approach and is already assumed in the spec.

**Implementation**:
```html
<link rel="preconnect" href="https://fonts.googleapis.com" />
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin />
<link
  href="https://fonts.googleapis.com/css2?family=Playfair+Display:wght@300&display=swap"
  rel="stylesheet"
/>
```

Scope to landing page via CSS class:
```css
.landing h1, .landing h2, .logotype {
  font-family: 'Playfair Display', serif;
  font-weight: 300;
}
```

---

## 7. GitHub URL Configuration

### Decision: Read the GitHub repository URL from `src/landing/config/site.ts`

**Rationale**: The spec requires that the GitHub URL is not hardcoded in templates. A single config file provides a typed source of truth. The URL can be set once during scaffolding and updated without touching component code.

**Alternatives considered**:
- Read from `package.json` `repository` field — possible but requires parsing JSON at build time and is less explicit
- Read from git remote at build time — fragile, requires git to be available during build (not guaranteed in CI)
- Environment variable — adds deployment complexity for a value that rarely changes

---

## 8. Package Manager Choice

### Decision: Use npm (not yarn)

**Rationale**: The existing .NET project does not use yarn. npm is the default Node.js package manager and reduces tooling requirements. Dockit uses yarn, but there's no technical reason to follow that choice.
