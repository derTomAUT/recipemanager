# Nutrition Estimates Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add per-recipe AI nutrition estimates with persistence, UI display, and AI debug logging.

**Architecture:** Extend the existing recipe aggregate with nutrition snapshot fields. Add one recipe controller endpoint that delegates AI call + parsing to a dedicated service. Reuse existing household AI settings and AI debug logging infrastructure.

**Tech Stack:** ASP.NET Core Web API, EF Core/PostgreSQL, Angular standalone components, existing RecipeService/AiDebugLogService.

---

### Task 1: Add failing parser tests

**Files:**
- Test: `backend/tests/RecipeManager.Tests/NutritionEstimateParserTests.cs`

### Task 2: Implement parser + DTOs

**Files:**
- Create: `backend/src/RecipeManager.Api/Services/NutritionEstimateParser.cs`

### Task 3: Persist nutrition fields

**Files:**
- Modify: `backend/src/RecipeManager.Api/Models/Recipe.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/RecipeDtos.cs`
- Modify: `backend/src/RecipeManager.Api/Data/AppDbContext.cs`
- Create migration in `backend/src/RecipeManager.Api/Migrations`

### Task 4: Add AI nutrition service + API endpoint

**Files:**
- Create: `backend/src/RecipeManager.Api/Services/RecipeNutritionService.cs`
- Modify: `backend/src/RecipeManager.Api/Services/AiOperation.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`

### Task 5: Frontend wiring

**Files:**
- Modify: `frontend/src/app/models/recipe.model.ts`
- Modify: `frontend/src/app/services/recipe.service.ts`
- Modify: `frontend/src/app/pages/recipe-detail/recipe-detail.component.ts`

### Task 6: Verification

**Commands:**
- `dotnet test backend/tests/RecipeManager.Tests/RecipeManager.Tests.csproj`
- `dotnet build backend/src/RecipeManager.Api/RecipeManager.Api.csproj -c Release --no-restore`
- `npm --prefix frontend run build`
