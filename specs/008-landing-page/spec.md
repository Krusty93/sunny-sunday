# Feature Specification: 008 Landing Page

**Feature Branch**: `008-landing-page`  
**Created**: 2026-05-13  
**Status**: Draft  
**Input**: User request: "Build a static landing page for Relego using Astro based on the Dockit Astro template"

## User Scenarios & Testing

### User Story 1 - Core Landing Page with Hero and Navigation (Priority: P1)

A prospective user discovers Relego through a search engine or GitHub link and lands on the homepage. They see a clear explanation of what Relego does, a hero image of a Kindle, and navigation to the GitHub repository.

**Why this priority**: The landing page is the first touchpoint for any new user. Without a clear hero, tagline, and navigation, visitors cannot understand the project or find the source code.

**Independent Test**: Open the landing page in a browser and verify the navbar, hero section with logotype/tagline/CTA, and hero image with gradient overlay all render correctly.

**Acceptance Scenarios**:

1. **Given** a user visits the landing page, **When** the page loads, **Then** they see the "relego." logotype in Playfair Display 300 in the navbar and hero section.
2. **Given** a user visits the landing page, **When** the page loads, **Then** they see the tagline "Periodic highlights recap, delivered to your Kindle. For free." alongside a "Get started" CTA button.
3. **Given** a user visits the landing page, **When** the page loads, **Then** they see a Kindle photo on the right half of the hero with a CSS gradient overlay that blends into the page background.
4. **Given** a user clicks the "View on GitHub" button in the navbar, **When** the link opens, **Then** they are taken to the project's GitHub repository URL (read from configuration, not hardcoded).
5. **Given** a user clicks "Get started", **When** the page scrolls, **Then** they arrive at the Getting Started steps section.

---

### User Story 2 - Getting Started Steps (Priority: P1)

A prospective user wants to understand how Relego works before investing time in setup. Three numbered steps explain the workflow at a glance.

**Why this priority**: Understanding the setup flow is the second most important factor in conversion after knowing what the tool does.

**Independent Test**: Open the landing page and verify three numbered cards appear with correct step titles and descriptions.

**Acceptance Scenarios**:

1. **Given** a user scrolls to the Getting Started section, **When** it renders, **Then** three numbered cards appear in order: (1) Sync, (2) Schedule, (3) Read.
2. **Given** step card 1, **When** it renders, **Then** it describes connecting a Kindle and running `relego sync`.
3. **Given** step card 2, **When** it renders, **Then** it describes the server scheduling a recap using spaced repetition.
4. **Given** step card 3, **When** it renders, **Then** it describes opening the recap on a Kindle like any other book.

---

### User Story 3 - Dark/Light Theme Toggle (Priority: P2)

A user prefers dark mode. They toggle the theme and the entire page adapts, including the hero image gradient overlay.

**Why this priority**: Theme support improves readability for users who prefer dark mode and demonstrates attention to detail. It is secondary to core content.

**Independent Test**: Toggle between dark and light modes and verify all sections, including the hero gradient, adapt correctly.

**Acceptance Scenarios**:

1. **Given** the page is in light mode, **When** the user clicks the theme toggle, **Then** the page switches to dark mode with appropriate background and text colors.
2. **Given** the page is in dark mode, **When** the hero image renders, **Then** the gradient overlay fades from the dark background color (not white) to transparent.
3. **Given** the user toggles the theme, **When** they refresh the page, **Then** the selected theme persists.

---

### User Story 4 - Why Relego Feature Cards (Priority: P2)

A user wants to understand Relego's differentiators. Four feature cards highlight key selling points.

**Why this priority**: Feature cards reinforce the value proposition but are supplementary to the hero and steps sections.

**Independent Test**: Open the landing page and verify four feature cards appear with correct titles and descriptions.

**Acceptance Scenarios**:

1. **Given** a user scrolls to the Why Relego section, **When** it renders, **Then** four feature cards are visible: "Built for e-ink", "Free & self-hosted", "No lock-in", "Privacy".
2. **Given** each feature card, **When** it renders, **Then** it contains a small icon and a short description written in third person without buzzwords.

---

### User Story 5 - FAQ Section (Priority: P2)

A user has questions about setup requirements, SMTP providers, or data privacy. The FAQ section answers common friction points using an accordion component.

**Why this priority**: FAQs reduce friction for users considering adoption, but most information is also available in the README.

**Independent Test**: Open the landing page and verify the FAQ accordion renders 5-6 questions that expand/collapse correctly.

**Acceptance Scenarios**:

1. **Given** a user scrolls to the FAQ section, **When** it renders, **Then** 5-6 accordion items are visible with questions as headers.
2. **Given** a collapsed FAQ item, **When** the user clicks it, **Then** the answer expands and is visible.
3. **Given** an expanded FAQ item, **When** the user clicks it again, **Then** the answer collapses.
4. **Given** the FAQ content, **When** a user reads it, **Then** it addresses: cloud account requirements, SMTP provider compatibility, recap frequency, Kindle model support, data storage location, and excluding books/highlights.

---

### User Story 6 - Explore CTAs and Footer (Priority: P3)

A user who has read the page wants to take the next step. An Explore section provides two CTA buttons and the MIT license badge. A minimal footer closes the page.

**Why this priority**: CTAs at the bottom provide a clear next action, but the page already has CTAs in the navbar and hero.

**Independent Test**: Scroll to the bottom and verify two CTA buttons and the MIT badge render correctly, and the footer displays "Relego · MIT License".

**Acceptance Scenarios**:

1. **Given** a user scrolls to the Explore section, **When** it renders, **Then** two buttons appear: "View on GitHub" (primary) and "Read the docs" (secondary).
2. **Given** the Explore section, **When** it renders, **Then** an MIT license badge is visible below the buttons.
3. **Given** the footer, **When** it renders, **Then** it displays "Relego · MIT License".

---

### User Story 7 - Accessibility Compliance (Priority: P2)

A user navigating with a screen reader or keyboard can access all page content and interactive elements.

**Why this priority**: Accessibility is a quality requirement that affects all users and is required by project standards.

**Independent Test**: Run a Lighthouse accessibility audit and verify the score meets WCAG AA compliance.

**Acceptance Scenarios**:

1. **Given** the landing page, **When** Lighthouse runs an accessibility audit, **Then** the score meets AA compliance.
2. **Given** images on the page, **When** they render, **Then** all images have descriptive `alt` attributes.
3. **Given** interactive elements (buttons, accordion, theme toggle), **When** a user navigates with the keyboard, **Then** all elements are reachable and operable via keyboard alone.
4. **Given** text and background colors, **When** contrast is measured, **Then** all combinations meet WCAG AA contrast ratios.

---

### Edge Cases

- **JavaScript disabled**: The page renders all static content correctly. Only the theme toggle and accordion interactivity are degraded.
- **Missing hero image**: The hero section renders the gradient overlay on a fallback background color without layout breakage.
- **Very narrow viewport (< 320px)**: The hero switches to a single-column layout with the image below the text. No horizontal scrollbar appears.
- **Very wide viewport (> 1920px)**: Content is centered with a max-width container. The hero image does not stretch beyond its natural resolution.
- **Slow network**: The page is fully static and loads without waiting for API calls. Google Fonts load asynchronously with `display=swap` to avoid blocking text rendering.
- **GitHub URL unavailable in config**: Build fails with a clear error message rather than rendering a broken link.

## Requirements

### Functional Requirements

- **FR-008-01**: The landing page MUST be built as a static Astro site based on the Dockit Astro template.
- **FR-008-02**: The landing page MUST live under `src/landing/` and be completely separate from the existing .NET solution.
- **FR-008-03**: The main page MUST be located at `src/landing/pages/index.astro`.
- **FR-008-04**: The navbar MUST display the "relego." logotype on the left and a dark/light theme toggle button plus a "View on GitHub" CTA on the right.
- **FR-008-05**: The GitHub repository URL MUST be read from configuration (not hardcoded).
- **FR-008-06**: The hero section MUST use a split layout with logotype, tagline, and CTA on the left and a Kindle photo with CSS gradient overlay on the right.
- **FR-008-07**: The hero CSS gradient overlay MUST adapt automatically to dark/light mode using CSS variables for the page background color.
- **FR-008-08**: The "Get started" CTA MUST scroll the page to the Getting Started section.
- **FR-008-09**: The Getting Started section MUST display three numbered cards: Sync, Schedule, Read.
- **FR-008-10**: The Why Relego section MUST display four feature cards: Built for e-ink, Free & self-hosted, No lock-in, Privacy.
- **FR-008-11**: The FAQ section MUST use an accordion component with 5-6 questions covering common user friction points.
- **FR-008-12**: The Explore section MUST display "View on GitHub" (primary) and "Read the docs" (secondary) CTA buttons plus an MIT license badge.
- **FR-008-13**: The footer MUST display "Relego · MIT License".
- **FR-008-14**: The "relego." logotype MUST be rendered as text in Playfair Display weight 300, not as an image.
- **FR-008-15**: Playfair Display MUST be applied to all h1 and h2 headings on the landing page, scoped so it does not affect other pages.
- **FR-008-16**: All body text, labels, and buttons MUST use the template's existing sans-serif font stack.
- **FR-008-17**: All user-facing copy MUST use "Relego" (not "SunnySunday").
- **FR-008-18**: Copy MUST be written in third person with short sentences, targeting a technical audience, and MUST NOT use the words "powerful", "seamless", "robust", "leverage", or "unlock".
- **FR-008-19**: The page MUST be fully static with no client-side JavaScript except the theme toggle.
- **FR-008-20**: All images MUST have descriptive `alt` attributes.
- **FR-008-21**: All interactive elements MUST be reachable and operable via keyboard.
- **FR-008-22**: Color contrast MUST meet WCAG AA requirements.
- **FR-008-23**: The hero image MUST be located at `src/landing/assets/hero-kindle.jpg`.
- **FR-008-24**: The following Dockit template sections MUST be removed: hero search bar, "Powerful features" grid, and dark/light mode showcase section.

## Success Criteria

### Measurable Outcomes

- **SC-008-01**: A first-time visitor understands what Relego does within 10 seconds of landing on the page (validated by the hero section containing the logotype, tagline, and a clear CTA).
- **SC-008-02**: The page achieves a Lighthouse Accessibility score of 90 or above.
- **SC-008-03**: The page loads in under 2 seconds on a standard broadband connection (fully static, no API calls).
- **SC-008-04**: All seven page sections render correctly in both light and dark mode across common viewport widths (mobile, tablet, desktop).
- **SC-008-05**: 100% of interactive elements (theme toggle, accordion, CTA buttons, navigation links) are operable via keyboard alone.
- **SC-008-06**: The page renders all static content without JavaScript enabled (theme toggle and accordion degrade gracefully).
- **SC-008-07**: All FAQ questions expand and collapse correctly on click.

## Assumptions

- The Dockit Astro template provides reusable components for step cards, feature cards, and accordion elements that can be adapted for this page.
- The hero image (`hero-kindle.jpg`) will be provided separately and placed at `src/landing/assets/` before the page is deployed.
- Google Fonts (Playfair Display) is acceptable as an external dependency for typography.
- The "Read the docs" CTA will link to a placeholder path (`/docs`) until documentation is built; the link target can be updated later.
- The landing page does not require a build integration with the .NET solution — it is built and deployed independently.
- Single-page design — no additional routes or subpages beyond `index.astro`.
- Playwright MCP is available in the dev environment for E2E testing of the landing page.
- The landing page has no backend dependencies; it is purely static HTML/CSS/JS.
