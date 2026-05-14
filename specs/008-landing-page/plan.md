# Implementation Plan: Landing Page

**Branch**: `008-landing-page` | **Date**: 2026-05-14 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/008-landing-page/spec.md`

## Summary

Build a static landing page for Relego using Astro (without Starlight) and Tailwind CSS, deployed to GitHub Pages. The page is a single `index.astro` file with seven sections: Navbar, Hero, Getting Started (3 step cards), Why Relego (4 feature cards), FAQ (accordion), Explore (CTAs + MIT badge), and Footer. Components are inspired by the Dockit Astro template but reimplemented as standalone Astro components. Dark/light theme toggle with localStorage persistence. Playwright E2E tests organized by interaction type. A dedicated GitHub Actions CI pipeline handles build and deployment.

## Technical Context

**Language/Version**: TypeScript / Astro 5.x / Node.js 24
**Primary Dependencies**: Astro, @tailwindcss/vite, Playwright, @axe-core/playwright
**Storage**: N/A — fully static site, no database
**Testing**: Playwright E2E tests (`@playwright/test`)
**Target Platform**: GitHub Pages (static hosting), all modern browsers
**Project Type**: Static website (single-page marketing site)
**Performance Goals**: Page load < 2 seconds on broadband (SC-008-03), Lighthouse Accessibility ≥ 90 (SC-008-02)
**Constraints**: Fully static (no SSR), no client-side JS except theme toggle, no Starlight dependency
**Scale/Scope**: Single page, 7 sections, 5 E2E test files

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Client/Server Separation | **PASS** | Landing page is an independent static site; does not affect CLI or server |
| II. CLI-First, No GUI | **N/A** | Landing page is a marketing site, not a management GUI; does not replace CLI |
| III. Zero-Config Onboarding | **PASS** | Landing page directs users to GitHub/docs; no config needed for the page itself |
| IV. Local Processing Only | **PASS** | Fully static, no third-party data services (Google Fonts is a CDN, not a data service) |
| V. Tests Ship with Code | **PASS** | Playwright E2E tests included phase-by-phase |
| VI. Simplicity / YAGNI | **PASS** | Single page, no CMS, no content collections, no multi-language |
| Tech: C# / .NET 10 only | **JUSTIFIED** | Landing page uses TypeScript/Astro — a separate tech stack is necessary for a static site; the .NET solution has no static site generation capability. See Complexity Tracking. |
| Tech: Docker distribution | **N/A** | Landing page is deployed to GitHub Pages, not distributed via Docker |
| Exclusion: No web UI | **JUSTIFIED** | This is a marketing landing page, not a management UI/dashboard for the application. It does not expose any server functionality or replace CLI commands. See Complexity Tracking. |

**Post-design re-check**: All gates still pass. The landing page is completely decoupled from the .NET solution — separate directory, separate build, separate CI pipeline. No .NET code is affected. The TypeScript/Astro stack is justified because .NET has no static site generation equivalent suitable for a marketing page.

## Project Structure

### Documentation (this feature)

```text
specs/008-landing-page/
├── plan.md              # This file
├── research.md          # Dockit template analysis, Astro conventions, deployment decisions
├── data-model.md        # Site configuration and content structures
├── quickstart.md        # Developer quick-start guide
└── tasks.md             # Implementation tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/landing/                            # Astro project root
├── astro.config.mjs                    # Astro + Tailwind configuration
├── package.json                        # Node.js dependencies
├── package-lock.json                   # Lockfile
├── tsconfig.json                       # TypeScript configuration
├── playwright.config.ts                # Playwright E2E test configuration
├── public/                             # Static assets served as-is
│   └── favicon.svg                     # Site favicon
├── assets/                             # Processed assets (images)
│   └── hero-kindle.jpg                 # Hero section Kindle photo
├── components/                         # Astro components
│   ├── Accordion.astro                 # FAQ accordion item
│   ├── Button.astro                    # Styled button (primary/outline variants)
│   ├── FeatureCard.astro               # Why Relego feature card
│   ├── Footer.astro                    # Minimal footer
│   ├── Navbar.astro                    # Top navigation bar
│   ├── Section.astro                   # Reusable section wrapper (title + slot)
│   ├── StepCard.astro                  # Getting Started numbered step card
│   └── ThemeToggle.astro               # Dark/light mode toggle button
├── config/
│   └── site.ts                         # Site configuration (GitHub URL, tagline, etc.)
├── layouts/
│   └── Layout.astro                    # Base HTML layout (head, fonts, theme script)
├── pages/
│   └── index.astro                     # The landing page (single page)
└── styles/
    └── global.css                      # Tailwind imports + custom CSS variables
└── tests/                              # Playwright E2E tests
    ├── navigation.spec.ts              # Navbar links, smooth scroll CTA, Explore buttons
    ├── theme.spec.ts                   # Dark/light toggle, gradient adaptation, persistence
    ├── content-sections.spec.ts        # Sections present/ordered, step cards, feature cards
    ├── faq.spec.ts                     # Accordion expand/collapse
    └── accessibility.spec.ts           # axe-core audit, keyboard nav, alt text

.github/workflows/
└── landing-page.yml                    # CI: build, test, deploy to GitHub Pages
```

**Structure Decision**: The landing page is a standalone Astro project under `src/landing/`, completely independent from the .NET solution (`src/SunnySunday.slnx`). It has its own `package.json`, build toolchain, and CI pipeline. This follows the separation of concerns principle — the marketing site and the application are different artifacts with different tech stacks and deployment targets.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Non-C#/.NET tech stack (TypeScript/Astro) | Static site generation for a marketing page | .NET has no equivalent static site generator; Razor Pages would require a server. A static site is the correct tool for a marketing page hosted on GitHub Pages. |
| "No web UI" exclusion | This is a marketing landing page, not a management dashboard | The constitution excludes *management* web UIs that replace CLI commands. A marketing page that links to GitHub and docs is not a management interface. |
