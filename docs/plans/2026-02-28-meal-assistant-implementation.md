# Meal Assistant + Household Locale Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add a dedicated AI meal assistant that returns top 3 seasonal suggestions and add household coordinate selection via interactive map to make hemisphere handling explicit.

**Architecture:** Extend household settings with stored coordinates and expose them via existing settings API. Add a backend meal-assistant service and endpoint that combines household recipes, preferences/allergens, and season context, uses household AI settings for ranking, and falls back to deterministic ranking. Add a frontend `/meal-assistant` page and map picker in household settings.

**Tech Stack:** ASP.NET Core + EF Core + xUnit on backend, Angular standalone + Leaflet + Vitest on frontend.

---

### Task 1: Add failing backend tests for season and fallback behavior

**Files:**
- Create: `backend/tests/RecipeManager.Api.Tests/MealAssistantServiceTests.cs`
- Test: `backend/tests/RecipeManager.Api.Tests/MealAssistantServiceTests.cs`

**Step 1: Write failing tests**
- season resolves correctly for northern/southern hemisphere by month.
- allergens are hard-excluded from deterministic fallback.
- fallback returns max 3 suggestions.

**Step 2: Run test to verify it fails**
Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj --filter MealAssistantServiceTests`
Expected: FAIL because service does not exist yet.

### Task 2: Implement backend locale fields and settings API support

**Files:**
- Modify: `backend/src/RecipeManager.Api/Models/Household.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/HouseholdSettingsDtos.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/HouseholdController.cs`
- Modify: `backend/src/RecipeManager.Api/Data/AppDbContext.cs`
- Create: `backend/src/RecipeManager.Api/Migrations/<timestamp>_AddHouseholdCoordinates.cs`

**Step 1: Add nullable `Latitude` and `Longitude` to household model**
- add range-safe persistence (nullable decimal/double).

**Step 2: Extend settings DTOs and update controller**
- include coords in GET response.
- accept coords in PUT payload and validate ranges (`lat -90..90`, `lng -180..180`).

**Step 3: Add EF migration**
- add columns for coordinates.

### Task 3: Implement backend meal assistant service + endpoint

**Files:**
- Create: `backend/src/RecipeManager.Api/Services/MealAssistantService.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/RecipeDtos.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs`
- Test: `backend/tests/RecipeManager.Api.Tests/MealAssistantServiceTests.cs`

**Step 1: Implement service**
- load recipes/preferences/cook history.
- filter allergens hard.
- compute seasonal context from household coordinates.
- implement deterministic fallback ranking.
- implement AI ranking call using household AI settings (OpenAI/Anthropic).
- validate AI-selected IDs against candidate IDs and coerce to top 3 unique.

**Step 2: Add endpoint + DTOs**
- `POST /api/recipes/meal-assistant`.
- request: free-form prompt.
- response: season context + top 3 suggestions + warnings.

**Step 3: Register service and satisfy tests**
- wire DI in `Program.cs`.
- run focused tests until green.

### Task 4: Add frontend map picker to household settings

**Files:**
- Modify: `frontend/src/app/services/household-settings.service.ts`
- Modify: `frontend/src/app/pages/household-settings/household-settings.component.ts`
- Modify: `frontend/package.json`
- Modify: `frontend/package-lock.json`

**Step 1: Add settings service fields**
- add latitude/longitude to frontend settings interfaces and update payload typing.

**Step 2: Integrate Leaflet picker**
- render map in settings page.
- allow click/tap to set marker and coordinates.
- include clear/reset action.
- persist coords through existing save flow.

### Task 5: Add meal assistant page and navigation

**Files:**
- Create: `frontend/src/app/pages/meal-assistant/meal-assistant.component.ts`
- Modify: `frontend/src/app/services/recipe.service.ts`
- Modify: `frontend/src/app/models/recipe.model.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Modify: `frontend/src/app/app.ts`
- Modify: `frontend/src/app/app.html`

**Step 1: Add API model/service method**
- request/response types for meal assistant.
- service method to call backend endpoint.

**Step 2: Build dedicated page**
- free-form prompt input.
- submit + loading/error handling.
- render top 3 with short reasons and recipe links.
- show seasonal context text.

**Step 3: Route + navbar integration**
- add `/meal-assistant` route.
- add consistent nav icon entry.

### Task 6: Verification

**Files:**
- Modify (as needed): any files touched in tasks above.

**Step 1: Backend verification**
Run:
- `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`

Expected: PASS.

**Step 2: Frontend verification**
Run:
- `npm run build` in `frontend`
- `npx vitest run` for any new/changed frontend tests if added.

Expected: PASS (allow existing known budget warnings).

**Step 3: Final consistency check**
Run:
- `git status --short`

Expected: only intended file changes.

