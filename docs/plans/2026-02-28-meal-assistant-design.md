# Meal Assistant + Household Locale Design

## Summary
Add a dedicated AI meal assistant that proposes top 3 meals from the household recipe catalog using free-form prompts, preferences, allergens, and seasonality. Add household locale (interactive map picker) so season logic is based on explicit coordinates rather than hemisphere guessing.

## Goals
- Provide `Top 3` meal suggestions with short reasons from the recipes already available in the household.
- Use the existing household AI provider/model/key from household settings.
- Respect hard constraints (allergens) before AI selection.
- Use seasonality informed by household coordinates and current month.
- Add a dedicated frontend page for the assistant.
- Add map-based coordinate selection in household settings.

## Non-Goals
- No multi-turn memory/chat history in v1.
- No external weather APIs.
- No separate per-user locale override (household-level only in v1).

## Product Behavior
- New page: `/meal-assistant`.
- User enters free-form prompt (for example: "something quick and spicy tonight").
- Backend returns top 3 suggestions:
  - recipe id/title
  - short reason
  - optional soft warning (for disliked ingredients)
- If AI call fails or AI settings are missing, backend returns deterministic fallback top 3.

## Locale and Seasonality
- Household stores `latitude` and `longitude` (nullable).
- Owner sets coordinates in household settings via interactive map picker.
- Season is derived from coordinates + current month:
  - North: Dec-Feb Winter, Mar-May Spring, Jun-Aug Summer, Sep-Nov Autumn
  - South: inverted
- If no coordinates configured, assistant returns a clear warning and falls back to neutral season behavior.

## Backend Design
- Extend `Household` with nullable `Latitude` and `Longitude`.
- Extend household settings DTOs:
  - return coords from `GET /api/household/settings`
  - accept coords in `PUT /api/household/settings`
- Add `MealAssistantService`:
  - load household recipes, preferences, cook history
  - hard filter allergens
  - build candidate list and context summary
  - call OpenAI/Anthropic using existing household AI settings
  - constrain AI output to candidate IDs
  - validate/repair AI output; fallback deterministic ranking when needed
- Add API endpoint: `POST /api/recipes/meal-assistant`.

## Frontend Design
- New page component `meal-assistant`:
  - prompt input
  - submit action
  - loading/error states
  - top 3 suggestions with links to recipe details
  - seasonal context display
- Add route and navbar entry.
- Update household settings page:
  - embed Leaflet map
  - click to place/update marker
  - show selected coordinates
  - save through existing settings API.

## Testing Strategy
- Backend unit tests:
  - season calculation from month + latitude
  - fallback ranking and allergen hard filter
- Backend API/controller tests:
  - endpoint shape and fallback behavior
- Frontend:
  - component/service tests for meal assistant request/response rendering
  - build verification

