# Relego Landing Page — Copilot Prompt

## Context

You are building a static landing page for **Relego**, a self-hosted open source tool
that parses Kindle highlights and delivers periodic recap documents to a Kindle device.
You already have full context on the project from the repository (README, architecture,
CLI commands, Docker setup). Use that knowledge — do not invent features or copy
placeholder text.

N.B. SunnySunday and Relego are the same thing. Relego will replace SunnySunday as the project name, but the repo and some internal references still say SunnySunday. Use "Relego" in all user-facing copy, and do not worry about internal consistency for now.

---

## Base template

Use **Dockit Astro** as the starting point:
https://github.com/themefisher/dockit-astro

Set up the Astro project under `src/landing/`. This is a pure marketing landing page,
completely separate from any documentation. It does not use Starlight's doc sidebar or
search index.

---

## Target page structure

Keep only these sections, in this exact order:

1. **Navbar** — logo (left) + two items on the right: a dark/light theme toggle button
   and a "View on GitHub" CTA button that links to this repository's GitHub URL
   (read it from `package.json`, the git remote, or wherever it is already declared in
   the project — do not hardcode a placeholder URL).

2. **Hero** — full-width section with a split layout:
   - Left half: the "relego." logotype (see Typography section) + one-line tagline from
     the README ("Periodic highlights recap, delivered to your Kindle. For free.") + a
     primary CTA button ("Get started ↓") that scrolls to the steps section.
   - Right half: the hero image (`src/landing/assets/hero-kindle.jpg`, or the actual
     filename once the image is placed there). The image is a photo of a Kindle on a
     wooden table. Apply a CSS gradient overlay as a `::before` pseudo-element on the
     image container, fading from the page background color on the left edge to
     transparent toward the right, so the image blends into the white (or dark) page:
     ```css
     .hero-image-wrapper::before {
       content: '';
       position: absolute;
       inset: 0;
       background: linear-gradient(to right, var(--sl-color-bg) 0%, transparent 50%);
       z-index: 1;
     }
     ```
     Use whatever CSS variable Dockit/Starlight exposes for the page background
     (`--sl-color-bg` or equivalent). The gradient must adapt automatically when the
     user switches to dark mode — do not hardcode `white`.

3. **3 steps — Getting started** — three numbered cards derived from the "How it works"
   section of the README:
   - Step 1: Connect your Kindle and run `relego sync`
   - Step 2: The server schedules a recap using spaced repetition
   - Step 3: Open the recap on your Kindle like any other book

   Each card has a large step number, a short title, and one sentence of description.
   Use the existing Dockit step card component and styles.

4. **Why Relego** — four feature cards, each with a small icon and a short description.
   Map directly from the "Why Relego" bullets in the README, with this adjustment:
   - **Built for e-ink** (not "E-ink first" — frame it as a positive design choice,
     not a constraint)
   - Free and self-hosted
   - No lock-in
   - Privacy

5. **FAQ** — use the existing Dockit accordion component. Write 5–6 questions covering
   the most common friction points a new user would have, derived from the README and
   the Getting Started section. Good candidates:
   - Do I need a cloud account or subscription?
   - Which SMTP providers work? (address the Gmail/Outlook limitation explicitly)
   - How often are recaps sent?
   - Does it work on all Kindle models?
   - Where is my data stored?
   - How do I exclude a book or highlight?

6. **Explore** — a minimal section with two buttons side by side:
   "View on GitHub" (primary, same repo URL as the navbar) and "Read the docs"
   (secondary, links to `/docs` or wherever the documentation will live).
   Below the buttons, the MIT license badge.

7. **Footer** — minimal: "Relego · MIT License".

---

## Sections to remove from the Dockit template

Remove entirely — do not adapt, do not repurpose:

- The hero search bar and popular-links row
- The "Powerful features" four-item grid (the one with "Well organized", "Lightning
  fast", "Powerful search", "Very customized")
- The dark/light mode showcase section ("Switch easily from dark/light mode")

---

## Typography

Load **Playfair Display** from Google Fonts (weight 300 only):

```html
<link rel="preconnect" href="https://fonts.googleapis.com" />
<link
  href="https://fonts.googleapis.com/css2?family=Playfair+Display:wght@300&display=swap"
  rel="stylesheet"
/>
```

There is no logo image file. The logotype is rendered in HTML/CSS as the text
**"relego."** (with a trailing period) in Playfair Display Light 300. Use this
for both the navbar and the hero heading. Example:

```html
<span class="logotype">relego.</span>
```

```css
.logotype {
  font-family: 'Playfair Display', serif;
  font-weight: 300;
  letter-spacing: -0.01em;
}
```

Apply Playfair Display to all `h1` and `h2` elements on the landing page as well.
All other text (body, labels, buttons, captions) stays in the template's existing
sans-serif stack.

Do **not** apply Playfair to documentation pages if they share a layout — scope the
font rule to the landing page only (e.g. `.landing h1, .landing h2`).

---

## Tone and copy guidelines

- Write in third person, short sentences.
- Never use the words "powerful", "seamless", "robust", "leverage", or "unlock".
- Prefer concrete over abstract: "sends a document to your Kindle email address" beats
  "delivers content to your device".
- The audience is a technical user comfortable with Docker and a terminal. Skip
  beginner explanations, but do not assume they know what Send-to-Kindle is.

---

## Assets

- **Logo**: text-only, rendered in CSS as described in the Typography section.
- **Hero image**: already available at `src/landing/assets/hero-kindle.jpg` (or the
  actual filename — check the `src/landing/assets/` directory). Apply the gradient
  overlay as described in the Hero section above.
- **Favicon**: reuse the existing one if present in the repo; otherwise leave a
  `<!-- TODO: add favicon -->` comment.

---

## Output expectations

- The Astro project lives at `src/landing/`. The main page is
  `src/landing/src/pages/index.astro`.
- All content is in a single Astro page file where possible, with component imports for
  Dockit's existing accordion and step-card components.
- The page must be fully static — no client-side JS beyond what Dockit already ships
  (the theme toggle is the only exception).
- Ensure the page passes a basic Lighthouse accessibility check: all images have `alt`,
  all interactive elements are keyboard-reachable, color contrast meets WCAG AA.
