# Recipe Step Images Layout Design

**Date:** 2026-02-27  
**Owner:** Codex

## Goal
Show step images next to cooking instructions on desktop and as a mobile carousel, while keeping the hero image in the current gallery position. Step images are optional.

## Approach
- Rework the recipe detail page layout only (no backend changes).
- Map step images by `orderIndex`:
  - `orderIndex = 0` is the hero image.
  - Step image for step `i` uses `orderIndex = i + 1`.
- Desktop: two‑column step rows (image + text).
- Mobile: text first, then a horizontal scroll carousel for the step image (if present).

## UI Layout
- Keep existing hero image gallery section as‑is (still shows all images).
- In the instructions section:
  - For each step, locate the matching image by `orderIndex`.
  - Render image in a dedicated column on desktop.
  - On mobile, stack text and a carousel strip (single image per step) with horizontal scroll.

## Styling
- Use CSS grid for desktop step rows.
- Use `overflow-x: auto` and `scroll-snap` for mobile carousel behavior.
- No placeholder if image is missing.

## Testing
- Manual UI check:
  - Desktop: step image appears next to text when available.
  - Mobile: step image appears in a horizontal strip under the step text.
  - Steps without images display text only.
