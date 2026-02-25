# AI Import Images Design

**Date:** 2026-02-25  
**Owner:** Codex

## Goal
During URL import, download and store the most relevant recipe images: one hero image showing the final dish and up to 20 step images. Use the configured AI provider/model (vision-capable) to analyze image bytes and classify relevance and step order.

## Non-Goals
- No separate vision model setting.
- No image editing, resizing, or OCR.
- No changes to frontend UI beyond showing images once saved.

## Architecture
- Extend URL import flow to extract candidate image URLs (JSON-LD `image`, OpenGraph `og:image`, `<img>` inside article/main).
- Download candidate image bytes with size/type filters and dedupe.
- If household AI model supports vision, call AI with image bytes and minimal text context to select:
  - `heroImageUrl`
  - `stepImages`: list of `{url, stepIndex?}` (max 20)
- Store selected images using `IStorageService` and create `RecipeImage` records:
  - Hero set as `IsTitleImage=true`
  - Step images stored with `OrderIndex` and optional step mapping if possible.
- If AI fails or model lacks vision, fallback to a single hero image (JSON-LD or `og:image`) or skip image import.

## Data Flow
1. Fetch HTML and parse JSON-LD.
2. Gather candidate image URLs.
3. Download bytes, apply filters.
4. Call AI to classify hero + steps.
5. Store selected images and attach to recipe.

## Error Handling
- Any AI failure: skip image import, still return recipe draft.
- Any image download failure: skip that image only.
- Enforce max bytes per image and max candidates to limit cost.

## Testing
- Manual: import recipe with multiple images; verify hero + step images saved.
- Manual: import from page without images; verify no failures.
