# Recipe Source URL Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Persist the recipe source URL from URL import and display it under the recipe header.

**Architecture:** Add `SourceUrl` to the recipe model and DTOs, pipe it from import draft into create request, and render it on the detail page. Keep it read-only by excluding it from update requests.

**Tech Stack:** ASP.NET Core, EF Core, Angular, RxJS.

---

### Task 1: Add failing backend test for source URL in draft

**Files:**
- Modify: `backend/tests/RecipeManager.Api.Tests/RecipeImportServiceTests.cs`

**Step 1: Write the failing test**

```csharp
[Fact]
public async Task ExtractDraftFromHtml_IncludesSourceUrl()
{
    var html = @"<html><head>
<script type=""application/ld+json"">
{""@context"":""https://schema.org"",""@type"":""Recipe"",""name"":""Test Cake""}
</script>
</head><body></body></html>";

    var dataProtectionProvider = DataProtectionProvider.Create("RecipeManager.Tests");
    var aiSettings = new HouseholdAiSettingsService(dataProtectionProvider);
    var aiImport = new AiRecipeImportService(new TestHttpClientFactory(), aiSettings, NullLogger<AiRecipeImportService>.Instance);
    var service = new RecipeImportService(
        new TestHttpClientFactory(),
        aiImport,
        new ImageFetchService(new TestHttpClientFactory()),
        new TestStorageService());

    var household = new Household();
    var url = "https://example.com/recipe";

    var draft = await service.ExtractDraftFromHtmlAsync(html, url, household);

    Assert.Equal(url, draft.SourceUrl);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj --filter FullyQualifiedName~ExtractDraftFromHtml_IncludesSourceUrl`
Expected: FAIL because `SourceUrl` is missing on DTO.

---

### Task 2: Add SourceUrl to backend model/DTOs and map in import/create/detail

**Files:**
- Modify: `backend/src/RecipeManager.Api/Models/Recipe.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/RecipeImportDtos.cs`
- Modify: `backend/src/RecipeManager.Api/DTOs/RecipeDtos.cs`
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`
- Create: `backend/src/RecipeManager.Api/Migrations/2026XXXXXX_AddRecipeSourceUrl.cs` (generated)
- Modify: `backend/src/RecipeManager.Api/Migrations/RecipeManager.ApiModelSnapshot.cs` (generated)

**Step 1: Add SourceUrl to model**

```csharp
public string? SourceUrl { get; set; }
```

**Step 2: Add SourceUrl to RecipeDraftDto and Create/Detail DTOs**

```csharp
public record RecipeDraftDto(
    string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientDto> Ingredients,
    List<StepDto> Steps,
    List<string> Tags,
    List<ImportedImageDto> ImportedImages,
    List<CandidateImageDto> CandidateImages,
    double? ConfidenceScore,
    List<string> Warnings,
    string? SourceUrl
);
```

```csharp
public record CreateRecipeRequest(
    ...,
    List<ImportedImageDto>? ImportedImages = null,
    string? SourceUrl = null
);

public record RecipeDetailDto(
    ...,
    Guid CreatedByUserId,
    string? SourceUrl
);
```

**Step 3: Set SourceUrl in import**

In `RecipeImportService.ExtractDraftFromHtmlAsync`, set `SourceUrl` on the draft to the input `url`.

**Step 4: Persist SourceUrl on create**

In `RecipeController.CreateRecipe`, set:

```csharp
SourceUrl = request.SourceUrl
```

and include `SourceUrl` in the `RecipeDetailDto` response.

**Step 5: Include SourceUrl in GetRecipe**

Add `recipe.SourceUrl` to `RecipeDetailDto` mapping in `GetRecipe`, `UpdateRecipe` response, and any other `RecipeDetailDto` creation.

**Step 6: Add migration**

Run: `dotnet ef migrations add AddRecipeSourceUrl -p backend/src/RecipeManager.Api/RecipeManager.Api.csproj`

**Step 7: Run test to verify it passes**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj --filter FullyQualifiedName~ExtractDraftFromHtml_IncludesSourceUrl`
Expected: PASS

**Step 8: Commit**

```bash
git add backend/src/RecipeManager.Api/Models/Recipe.cs \
  backend/src/RecipeManager.Api/DTOs/RecipeImportDtos.cs \
  backend/src/RecipeManager.Api/DTOs/RecipeDtos.cs \
  backend/src/RecipeManager.Api/Services/RecipeImportService.cs \
  backend/src/RecipeManager.Api/Controllers/RecipeController.cs \
  backend/src/RecipeManager.Api/Migrations
git commit -m "feat: persist recipe source url"
```

---

### Task 3: Add failing frontend test for passing sourceUrl on create

**Files:**
- Create: `frontend/src/app/pages/recipe-editor/recipe-editor.component.spec.ts`

**Step 1: Write the failing test**

```ts
it('includes sourceUrl when saving imported draft', () => {
  // arrange draft with sourceUrl and spy on createRecipe payload
});
```

**Step 2: Run test to verify it fails**

Run: `npm test -- --watch=false --include=**/recipe-editor.component.spec.ts`
Expected: FAIL because sourceUrl not wired.

---

### Task 4: Wire sourceUrl through frontend models and UI

**Files:**
- Modify: `frontend/src/app/models/recipe.model.ts`
- Modify: `frontend/src/app/pages/recipe-editor/recipe-editor.component.ts`
- Modify: `frontend/src/app/pages/recipe-detail/recipe-detail.component.ts`
- Modify: `frontend/src/styles.css`

**Step 1: Add sourceUrl to models**

```ts
export interface RecipeDetail extends Recipe {
  ...
  sourceUrl?: string;
}

export interface CreateRecipeRequest {
  ...
  sourceUrl?: string;
}

export interface RecipeDraft {
  ...
  sourceUrl?: string;
}
```

**Step 2: Apply draft sourceUrl**

In `applyDraft`, set:

```ts
this.recipe.sourceUrl = draft.sourceUrl;
```

**Step 3: Ensure create request includes sourceUrl**

`CreateRecipeRequest` should already include it via spread in `save()`.

**Step 4: Render on recipe detail**

Add below the `<h1>`:

```html
<div *ngIf="recipe.sourceUrl" class="source-url">{{ recipe.sourceUrl }}</div>
```

Add styles in `styles.css`:

```css
.source-url {
  font-size: 0.85rem;
  color: var(--muted);
  margin-top: 0.35rem;
  word-break: break-word;
}
```

**Step 5: Run frontend test to verify it passes**

Run: `npm test -- --watch=false --include=**/recipe-editor.component.spec.ts`
Expected: PASS

**Step 6: Commit**

```bash
git add frontend/src/app/models/recipe.model.ts \
  frontend/src/app/pages/recipe-editor/recipe-editor.component.ts \
  frontend/src/app/pages/recipe-detail/recipe-detail.component.ts \
  frontend/src/styles.css \
  frontend/src/app/pages/recipe-editor/recipe-editor.component.spec.ts
git commit -m "feat: show recipe source url"
```

---

### Task 5: Manual verification

**Step 1: Manual flow**
- Import a recipe via URL.
- Confirm the draft carries the URL.
- Save and open recipe detail.
- Verify URL appears under header.

**Step 2: Commit any fixes**

```bash
git add frontend/src/app/pages/recipe-detail/recipe-detail.component.ts
git commit -m "fix: recipe source url display"
```
