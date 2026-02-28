# Nutrition Estimates Design

Date: 2026-02-28
Scope: Phase 2 enhancement - per-recipe nutrition estimate

## Goal
Add per-recipe nutrition estimates on recipe detail using the household AI provider/model/key already configured in Household Settings.

## UX
- Recipe detail page gets an `Estimate Nutrition` button.
- After success, show a nutrition card with:
  - Per serving: calories, protein, carbs, fat
  - Total recipe: calories, protein, carbs, fat
  - Optional micronutrients: fiber, sugar, sodium
  - Metadata: source (`AI`), estimated timestamp, confidence note.
- If key/provider/model is missing or decryption fails, show actionable error message.

## Backend design
- Persist nutrition snapshot directly on `Recipe` (MVP-friendly):
  - `NutritionPerServingJson` (text)
  - `NutritionTotalJson` (text)
  - `NutritionEstimatedAtUtc` (timestamp)
  - `NutritionSource` (text)
  - `NutritionNotes` (text)
- Add endpoint: `POST /api/recipes/{id}/nutrition/estimate`.
- Endpoint behavior:
  - Household authorization and recipe ownership check.
  - Build prompt from recipe title, servings, ingredients.
  - Call OpenAI/Anthropic based on household settings.
  - Parse strict JSON object response.
  - Validate numeric ranges and normalize to 2 decimals.
  - Save snapshot to recipe and return updated recipe detail.

## AI logging
- Add `AiOperation.NutritionEstimate` to shared enum mapper.
- Log all requests/responses using `AiDebugLogService` for this endpoint.

## Error handling
- Missing AI settings: `400` with clear message.
- Key decryption failure: `400` with re-save instruction.
- AI non-2xx or invalid JSON: `502` + stable UI error text.
- Keep deterministic behavior: no auto-refresh loops, no background retries.

## Testing strategy
- Unit tests for parser/validator:
  - valid object parsing
  - code-fenced JSON parsing
  - invalid shape rejection
- Build verification:
  - backend build + tests
  - frontend build
