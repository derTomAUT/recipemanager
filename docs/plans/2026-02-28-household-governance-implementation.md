# Household Governance Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Implement invite expiry/regeneration, owner-safe member governance, activity feed, and browser e2e coverage.

**Architecture:** Extend household domain entities with invite lifecycle and activity logging while preserving existing invite-code join model. Enforce owner/member invariants at controller level, then expose new endpoints and bind UI sections in household settings.

**Tech Stack:** ASP.NET Core 10 + EF Core, Angular 21, Vitest, Playwright.

---

### Task 1: Backend schema + migrations
**Files**
- Modify: `backend/src/RecipeManager.Api/Models/Household.cs`
- Modify: `backend/src/RecipeManager.Api/Data/AppDbContext.cs`
- Create: `backend/src/RecipeManager.Api/Migrations/*_AddHouseholdGovernance.cs`

### Task 2: Backend DTOs + controller behavior
**Files**
- Modify: `backend/src/RecipeManager.Api/DTOs/HouseholdDtos.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/HouseholdController.cs`

### Task 3: Backend tests
**Files**
- Modify: `backend/tests/RecipeManager.Api.Tests/HouseholdControllerTests.cs`

### Task 4: Frontend service + household settings UI
**Files**
- Modify: `frontend/src/app/services/household-settings.service.ts`
- Modify: `frontend/src/app/pages/household-settings/household-settings.component.ts`
- Modify: `frontend/src/app/pages/household-setup/household-setup.component.ts`
- Add/modify tests under `frontend/src/app/services` and `frontend/src/app/pages/household-settings`

### Task 5: Browser e2e tests
**Files**
- Add Playwright config/package scripts.
- Add e2e specs for ownership rotation + invite expiry/regeneration.

### Task 6: Verification
**Commands**
- `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj -c Release`
- `npx vitest run ...targeted specs...`
- `npm run build -- --configuration pwa`
- `npx playwright test`
