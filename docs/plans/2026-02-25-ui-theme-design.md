# UI Theme Design (System‑Adaptive, Rich/Expressive)

**Date:** 2026-02-25  
**Owner:** Codex

## Goal
Refresh the UI with a modern, mobile‑friendly, system‑adaptive theme that feels rich and expressive while remaining readable and usable.

## Visual Direction: Culinary Studio
An editorial, food‑forward aesthetic with warm neutrals, vibrant culinary accents, and expressive typography.

## Typography
- **Display / Headings:** `Fraunces` (variable serif, expressive)
- **Body / UI:** `General Sans` (clean, modern sans)
- **Scale:** 1.25 modular scale, generous line-height for readability.

## Color System (System‑Adaptive)
**Light**
- Background: `#F7F2EC` (warm cream)
- Surface: `#FFF8F1`
- Text: `#1F1A17`
- Muted text: `#6F655D`
- Primary: `#D9502F` (tomato)
- Secondary: `#6E9F7A` (sage)
- Accent: `#E7B84B` (saffron)

**Dark**
- Background: `#141210`
- Surface: `#1C1916`
- Text: `#F3EDE6`
- Muted text: `#B5AAA1`
- Primary: `#FF6B47`
- Secondary: `#7ABF8D`
- Accent: `#FFC95C`

## Surfaces & Depth
- Large rounded cards (`16–20px` radius), soft shadow in light, subtle glow in dark.
- Light grain texture overlay for depth (optional).

## Motion
- Staggered card entrance (20–60ms offsets).
- Hover lift on cards, subtle scale on buttons.
- Reduced motion support with `prefers-reduced-motion`.

## Layout & Components
- **Home:** hero banner with gradient + CTA, recommendations as image‑led cards.
- **Cards:** image‑dominant, tag chips, quick actions.
- **Forms:** pill inputs, floating labels, section separators.
- **Navigation:** sticky bottom nav on mobile, top nav on desktop.

## Mobile Principles
- 48px minimum tap targets.
- Full‑width primary actions.
- Bottom navigation and collapsing header.

## Success Criteria
- UI feels modern and cohesive in both light/dark.
- Improved readability and scannability.
- Mobile layout is thumb‑friendly.
