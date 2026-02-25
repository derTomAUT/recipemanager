# Import Image Candidates Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Show all candidate images on the Edit page, allow hero selection, and delete temporary candidate images on navigation or save.

**Architecture:** URL import uploads candidate images as temporary files and returns them in the draft. Edit page shows candidates and allows hero selection. On save, backend persists selected images and deletes unselected temp files; on cancel/navigation, frontend calls cleanup to delete all temp files.

**Tech Stack:** ASP.NET Core, Angular standalone components, existing storage service.

---

### Task 1: Extend draft DTOs for candidate images

**Files:**
- Modify: `backend/src/RecipeManager.Api/DTOs/RecipeImportDtos.cs`
- Modify: `frontend/src/app/models/recipe.model.ts`

**Step 1: Add candidate image DTOs (backend)**

```csharp
public record CandidateImageDto(
    string Url,
    bool IsHeroCandidate,
    int OrderIndex
);
```

Add to `RecipeDraftDto`:

```csharp
List<CandidateImageDto> CandidateImages
```

**Step 2: Add candidate image types (frontend)**

```ts
export interface CandidateImageInput {
  url: string;
  isHeroCandidate: boolean;
  orderIndex: number;
}
```

Add to `RecipeDraft`:

```ts
candidateImages?: CandidateImageInput[];
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/DTOs/RecipeImportDtos.cs frontend/src/app/models/recipe.model.ts
git commit -m "feat: add candidate images to draft"
```

---

### Task 2: Upload candidates as temp and include in draft

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`
- Modify: `backend/src/RecipeManager.Api/Infrastructure/Storage/LocalFileStorageService.cs`

**Step 1: Store candidate images under a temp prefix**

When storing candidates, prefix filename with `temp_` and include in draft as `CandidateImageDto`.

**Step 2: Add helper to delete temp files**

Add method to storage to delete by URL (already exists), and use it for cleanup.

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs backend/src/RecipeManager.Api/Infrastructure/Storage/LocalFileStorageService.cs
git commit -m "feat: store candidate images as temp"
```

---

### Task 3: Add cleanup endpoint

**Files:**
- Create: `backend/src/RecipeManager.Api/Controllers/ImportCleanupController.cs`

**Step 1: Add endpoint**

```csharp
public record CleanupImagesRequest(List<string> TempUrls);

[HttpPost("cleanup")]
public async Task<IActionResult> Cleanup([FromBody] CleanupImagesRequest request)
{
    foreach (var url in request.TempUrls)
    {
        if (url.Contains("/uploads/temp_"))
        {
            await _storage.DeleteAsync(url);
        }
    }
    return Ok();
}
```

**Step 2: Commit**

```bash
git add backend/src/RecipeManager.Api/Controllers/ImportCleanupController.cs
git commit -m "feat: add import cleanup endpoint"
```

---

### Task 4: Edit page UI and hero selection

**Files:**
- Modify: `frontend/src/app/pages/recipe-editor/recipe-editor.component.ts`

**Step 1: Display candidate images**

Render `draft.candidateImages` as a grid with “Set as Hero” button.

**Step 2: Track selected hero**

Store selected hero URL and include in `CreateRecipeRequest.importedImages`.

**Step 3: Cleanup on cancel/navigation**

On cancel or route leave, call cleanup endpoint with temp URLs.

**Step 4: Commit**

```bash
git add frontend/src/app/pages/recipe-editor/recipe-editor.component.ts
git commit -m "feat: show candidate images and hero selection"
```

---

### Task 5: Verification

**Step 1: Manual**
- Import a recipe, verify candidate images show.
- Select hero and save; check only selected images persist.
- Cancel editor; verify temp images deleted.

**Step 2: Commit any fixes**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs frontend/src/app/pages/recipe-editor/recipe-editor.component.ts
git commit -m "test: verify candidate image flow"
```
