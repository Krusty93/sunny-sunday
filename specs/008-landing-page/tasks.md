# Tasks: Landing Page

**Input**: Design documents from `/specs/008-landing-page/`
**Prerequisites**: plan.md, spec.md, research.md, data-model.md, quickstart.md

**Tests**: Playwright E2E tests integrated phase by phase, grouped by interaction type. Tests are written alongside implementation in each phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story. Each phase delivers a working increment.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- All source paths are relative to repository root

---

## Phase 1: Setup (Project Scaffolding)

**Purpose**: Initialize the Astro project with Tailwind CSS, TypeScript, Playwright, and base layout

- [ ] T001 Scaffold Astro project at `src/landing/` using `npm create astro@latest -- --template minimal`, configure `package.json` with project name `@relego/landing`
- [ ] T002 Install dependencies: `@tailwindcss/vite`, `@playwright/test`, `@axe-core/playwright`, and configure `astro.config.mjs` with `site: 'https://krusty93.github.io'`, `base: '/sunny-sunday/'`, `srcDir: '.'` (eliminates nested `src/` folder), and Tailwind vite plugin in `src/landing/astro.config.mjs`
- [ ] T003 [P] Create `src/landing/tsconfig.json` with strict TypeScript configuration extending Astro's base config
- [ ] T004 [P] Create `src/landing/styles/global.css` with Tailwind imports (`@import "tailwindcss"`) and CSS custom properties for light/dark theme colors (background, text, accent) using `[data-theme='dark']` selectors
- [ ] T005 [P] Create site configuration in `src/landing/config/site.ts` exporting typed `siteConfig` object with fields: `name` ("Relego"), `logotype` ("relego."), `tagline`, `githubUrl` ("https://github.com/Krusty93/sunny-sunday"), `docsUrl` ("/docs"), `license` ("MIT") — per data-model.md
- [ ] T006 [P] Create `src/landing/public/favicon.svg` with a minimal placeholder favicon
- [ ] T007 Create base layout in `src/landing/layouts/Layout.astro`: HTML shell with `<!DOCTYPE html>`, `<html lang="en" data-theme="light">`, Google Fonts `<link>` for Playfair Display 300 with `display=swap` and `preconnect`, global CSS import, inline `<script>` in `<head>` that reads `localStorage.getItem('theme')` or `prefers-color-scheme` and sets `data-theme` before paint to avoid flash
- [ ] T008 Create empty `src/landing/pages/index.astro` that imports and uses `Layout.astro`, renders a placeholder heading to verify the setup works
- [ ] T009 [P] Create `src/landing/playwright.config.ts` with: `webServer` pointing to `npm run dev` on port 4321, base URL `http://localhost:4321/sunny-sunday/`, Chromium-only project, `tests/` test directory, retries 0 for local, screenshot on failure
- [ ] T010 Verify setup: run `cd src/landing && npm install && npm run dev` starts without errors, and `npm run build` produces output in `dist/`

**Checkpoint**: Astro project scaffolded with Tailwind, TypeScript, Playwright config, and base layout. Dev server runs and builds successfully.

---

## Phase 2: Navbar + ThemeToggle + Footer (Structural Shell)

**Purpose**: Build the page structural shell — navigation, theme switching, and footer — with E2E tests for navigation and theme behavior

**Stories covered**: US1 (navbar/navigation), US3 (theme toggle), US6 (footer)

### Implementation

- [ ] T011 [P] Create `src/landing/components/Button.astro` with props: `href` (string), `variant` ("primary" | "outline"), `class` (optional). Renders an `<a>` tag styled with Tailwind classes — primary variant uses accent background, outline variant uses border + transparent background. Both adapt to dark/light mode via CSS variables.
- [ ] T012 Create `src/landing/components/ThemeToggle.astro`: renders a `<button>` with sun/moon icon (inline SVG), `aria-label="Toggle theme"`. Client-side `<script>` toggles `data-theme` attribute on `<html>` between "light" and "dark", persists choice to `localStorage`. Icons swap based on current theme.
- [ ] T013 Create `src/landing/components/Navbar.astro`: fixed top navbar with "relego." logotype text (Playfair Display 300, links to page top) on the left; ThemeToggle and "View on GitHub" Button (outline variant, `href` from `siteConfig.githubUrl`, opens in new tab with `target="_blank" rel="noopener"`) on the right. Responsive: collapses gracefully on mobile. Imports `siteConfig` from `src/landing/config/site.ts`.
- [ ] T014 Create `src/landing/components/Footer.astro`: minimal footer displaying "Relego · MIT License" text centered, with subtle top border. Uses `siteConfig.license` for the license name.
- [ ] T015 Update `src/landing/pages/index.astro` to import and render Navbar at top and Footer at bottom, with a `<main>` element between them

### Tests

- [ ] T016 [P] Create `src/landing/tests/navigation.spec.ts`: test that the navbar renders with "relego." logotype text; test that "View on GitHub" link points to the configured GitHub URL and opens in a new tab (`target="_blank"`); test that clicking the logotype navigates to or stays at the top of the page
- [ ] T017 [P] Create `src/landing/tests/theme.spec.ts`: test that clicking the theme toggle switches `data-theme` on `<html>` between "light" and "dark"; test that the selected theme persists after page reload (check `localStorage` and `data-theme` attribute); test that the toggle button has an accessible `aria-label`
- [ ] T018 Run `cd src/landing && npx playwright install --with-deps chromium && npx playwright test tests/navigation.spec.ts tests/theme.spec.ts` — all tests must pass

**Checkpoint**: Page has a working navbar with GitHub link, theme toggle with persistence, and footer. Navigation and theme tests pass.

---

## Phase 3: Hero Section (US1 — Core Landing Page)

**Purpose**: Implement the hero section with logotype, tagline, CTA, and Kindle image with gradient overlay

**Goal**: A first-time visitor understands what Relego does within 10 seconds of landing on the page.

**Independent Test**: Open the landing page and verify the hero section renders logotype, tagline, CTA button, and hero image with gradient overlay.

### Implementation

- [ ] T019 [P] Add placeholder hero image at `src/landing/assets/hero-kindle.jpg` (use a sample image or placeholder; the real Kindle photo will be swapped in before deployment)
- [ ] T020 Create `src/landing/components/Section.astro` with props: `id` (string), `title` (optional string), `class` (optional string). Renders a `<section>` with the given `id`, optional `<h2>` title in Playfair Display 300, and a default `<slot>` for content. Applies consistent vertical padding and max-width container.
- [ ] T021 [US1] Implement hero section in `src/landing/pages/index.astro`: split layout with left column containing "relego." logotype as `<h1>` in Playfair Display 300, tagline paragraph from `siteConfig.tagline`, and "Get started" Button (primary variant, `href="#getting-started"` for smooth scroll); right column containing the hero image imported from `src/landing/assets/hero-kindle.jpg` via Astro's `<Image>` component with descriptive `alt` text, overlaid with a CSS gradient that fades from `var(--color-bg)` to transparent (adapts to dark/light mode automatically). Responsive: stacks vertically on mobile with image below text.
- [ ] T022 [US1] Add smooth scroll behavior via CSS `html { scroll-behavior: smooth; }` in `src/landing/styles/global.css` and add `scroll-margin-top` to sections to account for the fixed navbar height

### Tests

- [ ] T023 [US1] Add tests to `src/landing/tests/navigation.spec.ts`: test that clicking "Get started" CTA scrolls the page to the Getting Started section (verify `#getting-started` element is in viewport after click)
- [ ] T024 Run `cd src/landing && npx playwright test` — all existing tests plus new hero navigation test must pass

**Checkpoint**: Hero section renders with logotype, tagline, CTA (smooth-scrolls to Getting Started), and hero image with theme-adaptive gradient. All tests pass.

---

## Phase 4: Getting Started + Why Relego (US1, US2, US4)

**Purpose**: Implement the three Getting Started step cards and four Why Relego feature cards, with content-sections E2E tests

**Goal**: Users see a clear 3-step workflow and four differentiators that explain Relego's value proposition.

### Implementation

- [ ] T025 [P] [US2] Create `src/landing/components/StepCard.astro` with props: `number` (number), `title` (string), `description` (string). Renders a card with a large styled number, bold title, and description text. Adapts to dark/light mode.
- [ ] T026 [P] [US4] Create `src/landing/components/FeatureCard.astro` with props: `icon` (string — inline SVG markup), `title` (string), `description` (string). Renders a card with icon, title, and description. Adapts to dark/light mode.
- [ ] T027 [US2] Implement Getting Started section in `src/landing/pages/index.astro`: use Section component with `id="getting-started"` and title "Getting Started". Render three StepCard components in a responsive grid: (1) "Sync" — connect your Kindle and run `relego sync`, (2) "Schedule" — the server picks highlights using spaced repetition, (3) "Read" — open the recap on your Kindle like any other book. Content per spec.md US2 acceptance scenarios.
- [ ] T028 [US4] Implement Why Relego section in `src/landing/pages/index.astro`: use Section component with `id="why-relego"` and title "Why Relego". Render four FeatureCard components in a responsive grid: "Built for e-ink", "Free & self-hosted", "No lock-in", "Privacy". Each card has a small inline SVG icon and a short description written in third person without buzzwords. Content per spec.md US4 acceptance scenarios.

### Tests

- [ ] T029 Create `src/landing/tests/content-sections.spec.ts`: test that all sections appear in correct order (hero → getting-started → why-relego → faq → explore → footer) by checking `id` attributes; test that exactly 3 step cards render in the Getting Started section with titles "Sync", "Schedule", "Read"; test that exactly 4 feature cards render in the Why Relego section with titles "Built for e-ink", "Free & self-hosted", "No lock-in", "Privacy"
- [ ] T030 Run `cd src/landing && npx playwright test` — all tests must pass

**Checkpoint**: Getting Started (3 step cards) and Why Relego (4 feature cards) render correctly. Content sections test validates section order and card content. All tests pass.

---

## Phase 5: FAQ Section (US5)

**Purpose**: Implement the FAQ accordion and its E2E tests

**Goal**: Common user questions (cloud requirements, SMTP, frequency, Kindle models, data storage, excluding content) are answered in an expandable accordion.

**Independent Test**: Open the FAQ section and verify 5–6 accordion items expand and collapse on click.

### Implementation

- [ ] T031 [US5] Create `src/landing/components/Accordion.astro` with props: `question` (string), `answer` (string). Renders a `<details>` / `<summary>` element (native HTML accordion — no JS required). `<summary>` shows the question with a chevron indicator. The answer is the expandable content. Styled with Tailwind for spacing, borders, and dark/light mode. Accessible: keyboard-operable via native `<details>` behavior.
- [ ] T032 [US5] Implement FAQ section in `src/landing/pages/index.astro`: use Section component with `id="faq"` and title "FAQ". Render 5–6 Accordion components with questions and answers covering: (a) cloud account requirements, (b) SMTP provider compatibility, (c) recap frequency, (d) Kindle model support, (e) data storage location, (f) excluding books/highlights. Content per spec.md US5 acceptance scenarios and FR-008-11.

### Tests

- [ ] T033 Create `src/landing/tests/faq.spec.ts`: test that 5–6 FAQ accordion items render with question text visible; test that clicking a collapsed FAQ item expands the answer; test that clicking an expanded FAQ item collapses the answer; test that FAQ content addresses the required topics (check for key terms: "SMTP", "Kindle", "data", "frequency" or similar)
- [ ] T034 Run `cd src/landing && npx playwright test` — all tests must pass

**Checkpoint**: FAQ accordion renders with 5–6 items that expand/collapse correctly. FAQ tests pass. All previous tests still pass.

---

## Phase 6: Explore Section + Accessibility (US6, US7)

**Purpose**: Implement the Explore CTAs with MIT badge, and run the final accessibility audit

**Goal**: Bottom-of-page CTAs guide users to next actions; the full page meets WCAG AA accessibility standards.

**Independent Test**: Scroll to bottom, verify two CTA buttons and MIT badge render. Run axe-core and verify no critical violations.

### Implementation

- [ ] T035 [US6] Implement Explore section in `src/landing/pages/index.astro`: use Section component with `id="explore"` and title "Explore". Render two Button components: "View on GitHub" (primary variant, `href` from `siteConfig.githubUrl`, `target="_blank" rel="noopener"`) and "Read the docs" (outline variant, `href` from `siteConfig.docsUrl`). Below the buttons, render an MIT license badge (text badge styled with Tailwind, not an image). Content per spec.md US6 acceptance scenarios.
- [ ] T036 [US7] Accessibility hardening pass across all components: verify all images have descriptive `alt` attributes (FR-008-20); verify all interactive elements (buttons, accordion, theme toggle, links) have appropriate ARIA labels and are keyboard-reachable (FR-008-21); verify color contrast meets WCAG AA (FR-008-22) by reviewing CSS variable color values in `global.css`; add `role` and `aria-*` attributes where needed

### Tests

- [ ] T037 [P] Add tests to `src/landing/tests/navigation.spec.ts`: test that the Explore section renders two buttons "View on GitHub" and "Read the docs"; test that "View on GitHub" links to the configured GitHub URL; test that the MIT license badge is visible
- [ ] T038 [P] Create `src/landing/tests/accessibility.spec.ts`: run axe-core audit via `@axe-core/playwright` on the full page and assert no critical or serious violations; test keyboard navigation through all interactive elements (Tab through navbar logotype, GitHub link, theme toggle, Get started CTA, accordion items, Explore buttons); test that all `<img>` elements have non-empty `alt` attributes
- [ ] T039 Run `cd src/landing && npx playwright test` — all tests must pass including the accessibility audit

**Checkpoint**: Explore section with CTAs and MIT badge renders. Accessibility audit passes. All E2E tests pass across all test files.

---

## Phase 7: CI Pipeline (GitHub Actions)

**Purpose**: Create a dedicated CI workflow for the landing page that builds, tests, and deploys to GitHub Pages

- [ ] T040 Create `.github/workflows/landing-page.yml` with: trigger on push to `main` with path filter `src/landing/**`; trigger on pull requests with same path filter (build + test only, no deploy); Node.js 24 setup; `cd src/landing && npm ci`; `npm run build`; `npx playwright install --with-deps chromium && npx playwright test`; on push to `main`: upload `dist/` as Pages artifact via `actions/upload-pages-artifact@v4` and deploy via `actions/deploy-pages@v4`; set `permissions: pages: write, id-token: write` for OIDC deployment
- [ ] T041 [P] Add `.github/workflows/landing-page.yml` concurrency group to prevent overlapping deployments: `concurrency: { group: "pages", cancel-in-progress: false }`
- [ ] T042 Verify CI workflow syntax: run `cd /workspaces/sunny-sunday && cat .github/workflows/landing-page.yml | head -5` and validate YAML structure is correct

**Checkpoint**: CI pipeline created. Builds, tests, and deploys the landing page to GitHub Pages on push to main.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final cleanup, documentation updates, and quickstart validation

- [ ] T043 [P] Review all user-facing copy in `src/landing/pages/index.astro` against spec.md FR-008-17 (use "Relego" not "SunnySunday") and FR-008-18 (third person, short sentences, no forbidden words: "powerful", "seamless", "robust", "leverage", "unlock")
- [ ] T044 [P] Verify Dockit template sections are removed per FR-008-24: no hero search bar, no "Powerful features" grid, no dark/light mode showcase section
- [ ] T045 [P] Verify the page renders all static content without JavaScript enabled per FR-008-19 and SC-008-06: only theme toggle and accordion interactivity should degrade
- [ ] T046 [P] Update `docs/ARCHITECTURE.md` to document the `src/landing/` component: its purpose (static marketing landing page), tech stack (Astro + Tailwind), deployment target (GitHub Pages), and separation from the .NET solution
- [ ] T047 Run full validation: `cd src/landing && npm run build && npx playwright test` — all tests pass. Validate that `quickstart.md` instructions in `specs/008-landing-page/quickstart.md` match the implemented project (dev server command, build command, test command, file paths).

---

## Dependencies & Execution Order

### Phase Dependencies

```
Phase 1: Setup ──────────────────────────────► Phase 2: Navbar + ThemeToggle + Footer
                                                         │
                                                         ▼
                                              Phase 3: Hero Section (US1)
                                                         │
                                                         ▼
                                              Phase 4: Getting Started (US2) + Why Relego (US4)
                                                         │
                                                         ▼
                                              Phase 5: FAQ (US5)
                                                         │
                                                         ▼
                                              Phase 6: Explore (US6) + Accessibility (US7)
                                                         │
                                              ┌──────────┴──────────┐
                                              ▼                     ▼
                                   Phase 7: CI Pipeline    Phase 8: Polish
```

- **Setup (Phase 1)**: No dependencies — start immediately
- **Structural Shell (Phase 2)**: Depends on Phase 1 — provides navbar, footer, and theme infrastructure for all subsequent phases
- **Hero (Phase 3)**: Depends on Phase 2 — needs navbar and layout
- **Content Sections (Phase 4)**: Depends on Phase 3 — needs Section component and page structure
- **FAQ (Phase 5)**: Depends on Phase 4 — builds on page content flow
- **Explore + Accessibility (Phase 6)**: Depends on Phase 5 — accessibility audit requires all content to be in place
- **CI Pipeline (Phase 7)**: Can start after Phase 6 — needs complete, testable site
- **Polish (Phase 8)**: Can run in parallel with Phase 7

### User Story Dependencies

- **US1 (Hero + Navigation)**: Phase 2 + Phase 3 — foundational, no story dependencies
- **US2 (Getting Started)**: Phase 4 — depends on US1 (hero CTA scrolls to this section)
- **US3 (Theme Toggle)**: Phase 2 — independent, tested early
- **US4 (Feature Cards)**: Phase 4 — independent of other stories, same phase as US2 for efficiency
- **US5 (FAQ)**: Phase 5 — independent, needs page structure
- **US6 (Explore + Footer)**: Phase 2 (footer) + Phase 6 (explore) — independent
- **US7 (Accessibility)**: Phase 6 — cross-cutting, requires all content present

### Within Each Phase

- Implementation tasks before corresponding test tasks
- Components before page integration
- Run all tests at the end of each phase as a gate

### Parallel Opportunities

**Phase 1**: T003, T004, T005, T006 can run in parallel (independent config/asset files)
**Phase 2**: T011 (Button) can run in parallel with T016/T017 test file creation (tests won't pass until components exist, but files can be scaffolded)
**Phase 4**: T025 (StepCard) and T026 (FeatureCard) can run in parallel (independent component files)
**Phase 6**: T037 and T038 can be written in parallel (independent test files)
**Phase 7/8**: Phase 7 (CI) and Phase 8 (Polish) tasks T043–T046 can run in parallel since they touch different files

---

## Implementation Strategy

### MVP Scope

Phase 1 + Phase 2 + Phase 3 deliver the minimum viable landing page: a user can visit the page, see the logotype, tagline, CTA, hero image, navigate to GitHub, and toggle the theme. This is sufficient for an initial deployment.

### Incremental Delivery

Each phase adds a complete, testable section. The page is deployable after any phase from Phase 2 onward — sections below the current implementation simply don't appear yet.

### Test Strategy

E2E tests are added in the phase where their target content is implemented:
- `navigation.spec.ts` — Phase 2 (initial), Phase 3 (CTA scroll), Phase 6 (Explore buttons)
- `theme.spec.ts` — Phase 2
- `content-sections.spec.ts` — Phase 4
- `faq.spec.ts` — Phase 5
- `accessibility.spec.ts` — Phase 6 (final pass over all content)

---

## Summary

| Metric | Value |
|--------|-------|
| **Total tasks** | 47 |
| **Phase 1 (Setup)** | 10 tasks |
| **Phase 2 (Structural Shell)** | 8 tasks |
| **Phase 3 (Hero — US1)** | 6 tasks |
| **Phase 4 (Content — US2, US4)** | 6 tasks |
| **Phase 5 (FAQ — US5)** | 4 tasks |
| **Phase 6 (Explore — US6, Accessibility — US7)** | 5 tasks |
| **Phase 7 (CI)** | 3 tasks |
| **Phase 8 (Polish)** | 5 tasks |
| **Parallel opportunities** | 16 tasks marked [P] |
| **MVP scope** | Phases 1–3 (24 tasks) |
