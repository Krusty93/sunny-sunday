# Data Model: Landing Page

**Feature**: 008-landing-page
**Phase**: 1 — Design
**Date**: 2026-05-14

## Overview

The landing page is a fully static site with no database, no API, and no runtime data. The "data model" for this feature is the **site configuration** and **content structure** that drives the page at build time.

---

## Site Configuration

### `src/landing/config/site.ts`

A single TypeScript module that exports typed configuration consumed by Astro components at build time.

```typescript
export const siteConfig = {
  name: "Relego",
  logotype: "relego.",
  tagline: "Periodic highlights recap, delivered to your Kindle. For free.",
  githubUrl: "https://github.com/Krusty93/sunny-sunday",
  docsUrl: "/docs",
  license: "MIT",
} as const;
```

| Field | Type | Description |
|-------|------|-------------|
| `name` | `string` | User-facing product name (always "Relego") |
| `logotype` | `string` | Text rendered as the logotype ("relego." with trailing period) |
| `tagline` | `string` | One-line description shown in the hero section |
| `githubUrl` | `string` | GitHub repository URL used in navbar CTA, Explore section |
| `docsUrl` | `string` | Documentation URL used in Explore section "Read the docs" button |
| `license` | `string` | License name shown in Explore section badge and footer |

**Validation**: The Astro build will fail with a clear error if `githubUrl` is empty or missing, satisfying the edge case requirement (FR-008-05).

---

## Content Structures

These are not database entities — they are the typed shapes used by Astro components to render content.

### Step Card

Used by the Getting Started section. Three cards, each representing a step in the workflow.

| Field | Type | Description |
|-------|------|-------------|
| `number` | `number` | Step number (1, 2, 3) |
| `title` | `string` | Short title (e.g., "Sync") |
| `description` | `string` | One sentence describing the step |

**Data source**: Hardcoded in `index.astro` as an array literal. Three items, matching FR-008-09.

### Feature Card

Used by the Why Relego section. Four cards highlighting differentiators.

| Field | Type | Description |
|-------|------|-------------|
| `icon` | `string` | Icon identifier or inline SVG |
| `title` | `string` | Feature name (e.g., "Built for e-ink") |
| `description` | `string` | Short description |

**Data source**: Hardcoded in `index.astro` as an array literal. Four items, matching FR-008-10.

### FAQ Item

Used by the FAQ section. Accordion component props.

| Field | Type | Description |
|-------|------|-------------|
| `question` | `string` | The question text (accordion header) |
| `answer` | `string` | The answer text (accordion body) |

**Data source**: Hardcoded in `index.astro` as an array literal. 5–6 items, matching FR-008-11.

---

## Astro Configuration

### `astro.config.mjs`

Key settings that affect build output:

| Setting | Value | Reason |
|---------|-------|--------|
| `site` | `https://krusty93.github.io` | Required for canonical URLs on GitHub Pages |
| `base` | `/sunny-sunday/` | Project page path prefix |
| `output` | `static` (default) | Fully static site, no SSR |
| `vite.plugins` | `[@tailwindcss/vite]` | Tailwind CSS processing |

### `tailwind.config` (via CSS)

Tailwind v4 uses CSS-based configuration. Dark mode is controlled via `[data-theme='dark']` selector, matching the theme toggle implementation.

---

## Asset Manifest

| Asset | Path | Notes |
|-------|------|-------|
| Hero image | `src/landing/assets/hero-kindle.jpg` | Kindle photo, gradient overlay applied via CSS |
| Favicon | `src/landing/public/favicon.svg` | Placeholder or reused from repo |
| Google Fonts | External CDN link | Playfair Display 300, loaded via `<link>` |

---

## Relationship to Existing Models

This feature has **no relationship** to the .NET domain models in `SunnySunday.Core` or `SunnySunday.Server`. The landing page is a completely independent static site with its own build toolchain (Node.js / Astro).
