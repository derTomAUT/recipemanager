# Recipe Source URL Design

**Date:** 2026-02-27  
**Owner:** Codex

## Goal
Persist the source URL when importing a recipe and display it on the recipe detail page under the header in a subtle style. The field is read-only once set.

## Approach
- Add a nullable `SourceUrl` field to `Recipe`.
- Propagate the field through DTOs and frontend models.
- Populate it from the import draft on create only (no updates).
- Render the URL under the recipe header as small, light text.

## Data Model
- `Recipe.SourceUrl` (nullable string).

## API/DTO Changes
- `RecipeDraftDto`: add `SourceUrl`.
- `CreateRecipeRequest`: add `SourceUrl`.
- `RecipeDetailDto`: add `SourceUrl`.
- `UpdateRecipeRequest`: no change (read-only behavior).

## Frontend Changes
- `RecipeDraft` model: add `sourceUrl?`.
- `CreateRecipeRequest` model: add `sourceUrl?`.
- `RecipeDetail` model: add `sourceUrl?`.
- Recipe import flow: pass `sourceUrl` from draft into create request.
- Recipe detail page: render source URL under header with small/light styling.

## Error Handling
- Validate URL format server-side if supplied (reuse existing import validation if needed).
- If missing, leave null and don’t render in UI.

## Testing
- Backend unit test: import draft includes `SourceUrl`.
- Frontend unit test: creation request includes `sourceUrl` when present.
