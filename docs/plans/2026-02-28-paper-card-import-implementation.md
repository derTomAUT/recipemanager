# Paper Card Import Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build a mobile-first paper-card import flow that parses two HelloFresh card images, lets users choose serving scale (2/3/4), and saves a full recipe including hero/step images.

**Architecture:** Add a backend draft-based parse/commit pipeline. Parse endpoint ingests two images, runs vision extraction, and returns an editable draft with serving variants. Commit endpoint validates selected serving and persists final recipe/images. Frontend adds a wizard + review UI and enforces serving selection before save.

**Tech Stack:** ASP.NET Core, EF Core/PostgreSQL, Angular standalone components, existing Recipe import pipeline, Playwright/Vitest, xUnit.

---

### Task 1: Backend Draft Model and Migration

**Files:**
- Create: `backend/src/RecipeManager.Api/Models/PaperCardImportDraft.cs`
- Modify: `backend/src/RecipeManager.Api/Data/AppDbContext.cs`
- Create: `backend/src/RecipeManager.Api/Migrations/<timestamp>_AddPaperCardImportDraft.cs`
- Modify: `backend/src/RecipeManager.Api/Migrations/AppDbContextModelSnapshot.cs`
- Test: `backend/tests/RecipeManager.Api.Tests/PaperCardImportTests.cs`

**Step 1: Write the failing test**
- Add test asserting draft can be saved/retrieved for household and contains serving variants.

**Step 2: Run test to verify it fails**
- Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj -c Release --filter PaperCardImport`
- Expected: FAIL due missing model/table.

**Step 3: Write minimal implementation**
- Add model/entity config + DbSet.
- Add migration for draft table and indexes (`HouseholdId`, `CreatedAtUtc`, `ExpiresAtUtc`).

**Step 4: Run test to verify it passes**
- Re-run filtered test.
- Expected: PASS.

**Step 5: Commit**
```bash
git add backend/src/RecipeManager.Api/Models/PaperCardImportDraft.cs backend/src/RecipeManager.Api/Data/AppDbContext.cs backend/src/RecipeManager.Api/Migrations
git commit -m "feat: add paper card import draft persistence"
```

### Task 2: Parse/Commit DTOs and Contracts

**Files:**
- Create: `backend/src/RecipeManager.Api/DTOs/PaperCardImportDtos.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/RecipeDtos.cs` (only if shared structures needed)
- Test: `backend/tests/RecipeManager.Api.Tests/PaperCardImportContractTests.cs`

**Step 1: Write the failing test**
- Add test for DTO serialization shape (`servingsAvailable`, `ingredientsByServings`, `selectedServings` required).

**Step 2: Run test to verify it fails**
- Run filtered test.
- Expected: FAIL due missing DTOs/validation.

**Step 3: Write minimal implementation**
- Add parse response + commit request DTOs with annotations.

**Step 4: Run test to verify it passes**
- Expected: PASS.

**Step 5: Commit**
```bash
git add backend/src/RecipeManager.Api/DTOs/PaperCardImportDtos.cs backend/tests/RecipeManager.Api.Tests/PaperCardImportContractTests.cs
git commit -m "feat: add paper card import API contracts"
```

### Task 3: Backend Parse Endpoint (Draft Creation)

**Files:**
- Create: `backend/src/RecipeManager.Api/Controllers/PaperCardImportController.cs`
- Create: `backend/src/RecipeManager.Api/Services/PaperCardVisionService.cs`
- Create: `backend/src/RecipeManager.Api/Services/PaperCardImagePreprocessService.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs` (DI)
- Test: `backend/tests/RecipeManager.Api.Tests/PaperCardImportControllerTests.cs`

**Step 1: Write the failing test**
- Test parse endpoint rejects missing front/back image.
- Test parse returns draft with servings and image URLs when mocked extractor returns data.

**Step 2: Run test to verify it fails**
- Expected: FAIL endpoint/service not implemented.

**Step 3: Write minimal implementation**
- Add controller action `POST /api/import/paper-card/parse`.
- Validate input files.
- Store temp images.
- Call extraction service (initial stub/mocked provider).
- Persist draft and return response.

**Step 4: Run test to verify it passes**
- Expected: PASS.

**Step 5: Commit**
```bash
git add backend/src/RecipeManager.Api/Controllers/PaperCardImportController.cs backend/src/RecipeManager.Api/Services backend/tests/RecipeManager.Api.Tests/PaperCardImportControllerTests.cs backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add paper card parse endpoint with draft creation"
```

### Task 4: Backend Commit Endpoint (Recipe Persistence)

**Files:**
- Modify: `backend/src/RecipeManager.Api/Controllers/PaperCardImportController.cs`
- Modify: `backend/src/RecipeManager.Api/Services/PaperCardVisionService.cs` (if mapping helpers kept here)
- Modify: `backend/src/RecipeManager.Api/Services/ImageStorageService.cs` (if needed for draft->permanent promotion)
- Test: `backend/tests/RecipeManager.Api.Tests/PaperCardImportControllerTests.cs`

**Step 1: Write the failing test**
- Test commit fails when `selectedServings` missing/invalid.
- Test commit fails for expired draft.
- Test commit persists recipe with selected serving ingredient list and steps.

**Step 2: Run test to verify it fails**
- Expected: FAIL due missing commit logic.

**Step 3: Write minimal implementation**
- Add `POST /api/import/paper-card/commit`.
- Load + validate draft ownership and expiry.
- Validate selected serving exists.
- Map to existing Recipe aggregate and persist.
- Promote hero/step images to recipe images.

**Step 4: Run test to verify it passes**
- Expected: PASS.

**Step 5: Commit**
```bash
git add backend/src/RecipeManager.Api/Controllers/PaperCardImportController.cs backend/tests/RecipeManager.Api.Tests/PaperCardImportControllerTests.cs
git commit -m "feat: add paper card commit endpoint"
```

### Task 5: Frontend Service Layer

**Files:**
- Create: `frontend/src/app/services/paper-card-import.service.ts`
- Create: `frontend/src/app/services/paper-card-import.service.spec.ts`
- Modify: `frontend/src/app/services/recipe-import.service.ts` (only if shared helper needed)

**Step 1: Write the failing test**
- Verify parse sends multipart with both files.
- Verify commit sends `draftId + selectedServings`.

**Step 2: Run test to verify it fails**
- Run: `cd frontend; npx vitest run src/app/services/paper-card-import.service.spec.ts`
- Expected: FAIL missing service.

**Step 3: Write minimal implementation**
- Add typed interfaces and API calls.

**Step 4: Run test to verify it passes**
- Expected: PASS.

**Step 5: Commit**
```bash
git add frontend/src/app/services/paper-card-import.service.ts frontend/src/app/services/paper-card-import.service.spec.ts
git commit -m "feat: add frontend paper card import service"
```

### Task 6: Frontend Mobile Wizard + Review UI

**Files:**
- Create: `frontend/src/app/pages/paper-card-import/paper-card-import.component.ts`
- Modify: `frontend/src/app/pages/home/home.component.ts` (add button/entry)
- Modify: `frontend/src/app/app.routes.ts` (new route)
- Test: `frontend/src/app/pages/paper-card-import/paper-card-import.component.spec.ts`

**Step 1: Write the failing test**
- UI requires front and back image before parse.
- UI blocks save until serving selected.
- UI sends edited data + serving on commit.

**Step 2: Run test to verify it fails**
- Expected: FAIL missing component behavior.

**Step 3: Write minimal implementation**
- Stepper/wizard UI:
  - capture or upload front/back
  - parse action
  - review extracted data
  - required serving selector
- show low-confidence warnings.

**Step 4: Run test to verify it passes**
- Expected: PASS.

**Step 5: Commit**
```bash
git add frontend/src/app/pages/paper-card-import frontend/src/app/pages/home/home.component.ts frontend/src/app/app.routes.ts
git commit -m "feat: add paper card import wizard and review flow"
```

### Task 7: Browser E2E (True Mobile-Oriented Flow)

**Files:**
- Create: `frontend/e2e/paper-card-import.spec.ts`
- Modify: `frontend/playwright.config.ts` (project/use settings if needed)

**Step 1: Write the failing test**
- E2E test covers:
  - opening import flow
  - uploading two images
  - parse response rendering
  - required serving selection enforcement
  - successful commit navigation

**Step 2: Run test to verify it fails**
- Run: `npm --prefix frontend run e2e -- --grep "paper card"`
- Expected: FAIL before implementation.

**Step 3: Write minimal implementation**
- Add route stubs + deterministic assertions.

**Step 4: Run test to verify it passes**
- Expected: PASS.

**Step 5: Commit**
```bash
git add frontend/e2e/paper-card-import.spec.ts frontend/playwright.config.ts
git commit -m "test: add browser e2e coverage for paper card import"
```

### Task 8: Final Verification + Docs

**Files:**
- Modify: `frontend/README.md` (new import flow section)
- Modify: `backend/src/RecipeManager.Api/README.md` or root docs if API docs live elsewhere
- Optional: `docs/plans/2026-02-28-paper-card-import-design.md` (link verification notes)

**Step 1: Run backend tests**
- `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj -c Release`

**Step 2: Run frontend unit tests**
- `cd frontend; npx vitest run src/app/services/paper-card-import.service.spec.ts src/app/pages/paper-card-import/paper-card-import.component.spec.ts`

**Step 3: Run frontend build**
- `npm --prefix frontend run build`

**Step 4: Run browser e2e**
- `npm --prefix frontend run e2e -- --grep "paper card"`

**Step 5: Commit docs/tidy**
```bash
git add frontend/README.md backend docs
git commit -m "docs: add paper card import flow and verification notes"
```
