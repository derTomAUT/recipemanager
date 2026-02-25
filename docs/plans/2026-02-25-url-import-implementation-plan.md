# URL Import for Recipes Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add URL-based recipe import that fetches and extracts a recipe draft on the backend, then opens the Recipe Editor pre-filled for user review and save.

**Architecture:** Introduce a backend import service and `POST /api/recipes/import/url` endpoint returning a `RecipeDraftDto` (JSON-LD first, heuristic fallback). Frontend adds an import UI on Recipe List, stores the draft in a shared service, and pre-fills the Recipe Editor when creating a new recipe.

**Tech Stack:** ASP.NET Core (Web API), EF Core, Angular standalone components, HttpClient, RxJS.

---

### Task 1: Add backend DTOs + tests scaffold

**Files:**
- Create: `backend/src/RecipeManager.Api/DTOs/RecipeImportDtos.cs`
- Create: `backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
- Create: `backend/tests/RecipeManager.Api.Tests/RecipeImportServiceTests.cs`
- Modify: `backend/src/RecipeManager.Api/RecipeManager.Api.csproj`

**Step 1: Create DTOs**

```csharp
namespace RecipeManager.Api.DTOs;

public record ImportRecipeUrlRequest(string Url);

public record RecipeDraftDto(
    string Title,
    string? Description,
    int? Servings,
    int? PrepMinutes,
    int? CookMinutes,
    List<IngredientDto> Ingredients,
    List<StepDto> Steps,
    List<string> Tags,
    double? ConfidenceScore,
    List<string> Warnings
);
```

**Step 2: Add HtmlAgilityPack package (for heuristics) and test dependencies**

```xml
<ItemGroup>
  <PackageReference Include="HtmlAgilityPack" Version="1.11.59" />
</ItemGroup>
```

Create test project `backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\RecipeManager.Api\RecipeManager.Api.csproj" />
  </ItemGroup>
</Project>
```

**Step 3: Write failing test for JSON-LD extraction**

```csharp
using RecipeManager.Api.Services;
using Xunit;

public class RecipeImportServiceTests
{
    [Fact]
    public void ExtractDraftFromHtml_ParsesJsonLdRecipe()
    {
        var html = @"
<html><head>
<script type=\"application/ld+json\">
{\"@context\":\"https://schema.org\",\"@type\":\"Recipe\",\"name\":\"Test Cake\",\"recipeIngredient\":[\"1 cup flour\"],\"recipeInstructions\":[\"Mix\",\"Bake\"],\"recipeYield\":\"4 servings\"}
</script>
</head><body></body></html>";

        var service = new RecipeImportService(null!); // HttpClient not needed for extraction
        var draft = service.ExtractDraftFromHtml(html, "https://example.com");

        Assert.Equal("Test Cake", draft.Title);
        Assert.Single(draft.Ingredients);
        Assert.Equal(2, draft.Steps.Count);
    }
}
```

**Step 4: Run test to verify it fails**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
Expected: FAIL with missing `RecipeImportService` or `ExtractDraftFromHtml`.

**Step 5: Commit**

```bash
git add backend/src/RecipeManager.Api/DTOs/RecipeImportDtos.cs backend/src/RecipeManager.Api/RecipeManager.Api.csproj backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj backend/tests/RecipeManager.Api.Tests/RecipeImportServiceTests.cs
git commit -m "test: scaffold import DTOs and JSON-LD extraction test"
```

---

### Task 2: Implement backend RecipeImportService (JSON-LD + fallback)

**Files:**
- Create: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`

**Step 1: Implement service with HTML fetch + extraction**

```csharp
using System.Text.Json;
using HtmlAgilityPack;
using RecipeManager.Api.DTOs;

namespace RecipeManager.Api.Services;

public class RecipeImportService
{
    private readonly IHttpClientFactory _httpClientFactory;

    public RecipeImportService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<RecipeDraftDto> ImportFromUrlAsync(string url)
    {
        var client = _httpClientFactory.CreateClient();
        var html = await client.GetStringAsync(url);
        return ExtractDraftFromHtml(html, url);
    }

    public RecipeDraftDto ExtractDraftFromHtml(string html, string url)
    {
        var jsonLdDraft = TryParseJsonLd(html);
        if (jsonLdDraft != null)
        {
            return jsonLdDraft with { ConfidenceScore = 0.8 };
        }

        var heuristicDraft = TryParseHeuristics(html);
        return heuristicDraft with { ConfidenceScore = 0.4, Warnings = new List<string> { "JSON-LD not found; used heuristic extraction." } };
    }

    private RecipeDraftDto? TryParseJsonLd(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var nodes = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
        if (nodes == null) return null;

        foreach (var node in nodes)
        {
            var json = node.InnerText;
            try
            {
                using var parsed = JsonDocument.Parse(json);
                var root = parsed.RootElement;
                var recipeElement = FindRecipeElement(root);
                if (recipeElement == null) continue;

                var title = recipeElement.Value.GetProperty("name").GetString() ?? "Imported Recipe";
                var description = recipeElement.Value.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                var ingredients = new List<IngredientDto>();
                if (recipeElement.Value.TryGetProperty("recipeIngredient", out var ing))
                {
                    foreach (var item in ing.EnumerateArray())
                    {
                        ingredients.Add(new IngredientDto(item.GetString() ?? string.Empty, null, null, null));
                    }
                }
                var steps = new List<StepDto>();
                if (recipeElement.Value.TryGetProperty("recipeInstructions", out var instr))
                {
                    foreach (var item in instr.EnumerateArray())
                    {
                        steps.Add(new StepDto(item.ValueKind == JsonValueKind.Object
                            ? item.GetProperty("text").GetString() ?? string.Empty
                            : item.GetString() ?? string.Empty, null));
                    }
                }

                return new RecipeDraftDto(
                    title,
                    description,
                    TryParseServings(recipeElement.Value),
                    TryParseMinutes(recipeElement.Value, "prepTime"),
                    TryParseMinutes(recipeElement.Value, "cookTime"),
                    ingredients,
                    steps,
                    new List<string>(),
                    null,
                    new List<string>()
                );
            }
            catch
            {
                // try next script block
            }
        }

        return null;
    }

    private static JsonElement? FindRecipeElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("@type", out var type) && type.GetString() == "Recipe")
                return root;

            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                {
                    if (item.TryGetProperty("@type", out var t) && t.GetString() == "Recipe")
                        return item;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (item.TryGetProperty("@type", out var t) && t.GetString() == "Recipe")
                    return item;
            }
        }
        return null;
    }

    private RecipeDraftDto TryParseHeuristics(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var ingredients = new List<IngredientDto>();
        var ingredientNodes = doc.DocumentNode.SelectNodes("//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'ingredient')]//li");
        if (ingredientNodes != null)
        {
            foreach (var node in ingredientNodes)
            {
                var text = node.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ingredients.Add(new IngredientDto(text, null, null, null));
                }
            }
        }

        var steps = new List<StepDto>();
        var stepNodes = doc.DocumentNode.SelectNodes("//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'instruction') or contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'direction')]//li");
        if (stepNodes != null)
        {
            foreach (var node in stepNodes)
            {
                var text = node.InnerText.Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    steps.Add(new StepDto(text, null));
                }
            }
        }

        return new RecipeDraftDto(
            "Imported Recipe",
            null,
            null,
            null,
            null,
            ingredients,
            steps,
            new List<string>(),
            null,
            new List<string>()
        );
    }

    private static int? TryParseServings(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("recipeYield", out var yield)) return null;
        var text = yield.GetString() ?? string.Empty;
        var digits = new string(text.TakeWhile(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : null;
    }

    private static int? TryParseMinutes(JsonElement recipe, string property)
    {
        if (!recipe.TryGetProperty(property, out var value)) return null;
        var text = value.GetString() ?? string.Empty;
        var digits = new string(text.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var result) ? result : null;
    }
}
```

**Step 2: Run tests**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
Expected: PASS.

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs
git commit -m "feat: add recipe URL import service"
```

---

### Task 3: Add import endpoint in RecipeController

**Files:**
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs`
- Modify: `backend/src/RecipeManager.Api/RecipeManager.Api.http`

**Step 1: Register HttpClient and service**

```csharp
builder.Services.AddHttpClient();
builder.Services.AddScoped<RecipeImportService>();
```

**Step 2: Add endpoint**

```csharp
[HttpPost("import/url")]
public async Task<ActionResult<RecipeDraftDto>> ImportFromUrl([FromBody] ImportRecipeUrlRequest request, [FromServices] RecipeImportService importService)
{
    if (string.IsNullOrWhiteSpace(request.Url)) return BadRequest("URL is required");
    if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        return BadRequest("Invalid URL");

    var userId = GetUserId();
    if (userId == null) return Unauthorized();

    var membership = await GetUserHouseholdAsync(userId.Value);
    if (membership == null) return BadRequest("User does not belong to a household");

    try
    {
        var draft = await importService.ImportFromUrlAsync(request.Url);
        return Ok(draft);
    }
    catch
    {
        return StatusCode(502, "Failed to import recipe from URL");
    }
}
```

**Step 3: Add HTTP scratch request**

```
### Import recipe from URL
POST {{RecipeManager.Api_HostAddress}}/api/recipe/import/url
Content-Type: application/json
Authorization: Bearer {{token}}

{ "url": "https://example.com/recipe" }
```

**Step 4: Commit**

```bash
git add backend/src/RecipeManager.Api/Program.cs backend/src/RecipeManager.Api/Controllers/RecipeController.cs backend/src/RecipeManager.Api/RecipeManager.Api.http
git commit -m "feat: add recipe URL import endpoint"
```

---

### Task 4: Add frontend draft models and services

**Files:**
- Modify: `frontend/src/app/models/recipe.model.ts`
- Create: `frontend/src/app/services/recipe-import.service.ts`
- Create: `frontend/src/app/services/recipe-draft.service.ts`

**Step 1: Add RecipeDraft model**

```ts
export interface RecipeDraft {
  title: string;
  description?: string;
  servings?: number;
  prepMinutes?: number;
  cookMinutes?: number;
  ingredients: IngredientInput[];
  steps: StepInput[];
  tags: string[];
  confidenceScore?: number;
  warnings: string[];
}
```

**Step 2: Add import service**

```ts
@Injectable({ providedIn: 'root' })
export class RecipeImportService {
  private apiUrl = `${environment.apiUrl}/recipe/import/url`;

  constructor(private http: HttpClient) {}

  importFromUrl(url: string): Observable<RecipeDraft> {
    return this.http.post<RecipeDraft>(this.apiUrl, { url });
  }
}
```

**Step 3: Add draft storage service**

```ts
@Injectable({ providedIn: 'root' })
export class RecipeDraftService {
  private draft: RecipeDraft | null = null;

  setDraft(draft: RecipeDraft) { this.draft = draft; }
  consumeDraft(): RecipeDraft | null {
    const value = this.draft;
    this.draft = null;
    return value;
  }
}
```

**Step 4: Commit**

```bash
git add frontend/src/app/models/recipe.model.ts frontend/src/app/services/recipe-import.service.ts frontend/src/app/services/recipe-draft.service.ts
git commit -m "feat: add recipe import draft models and services"
```

---

### Task 5: Add URL import UI on Recipe List page

**Files:**
- Modify: `frontend/src/app/pages/recipe-list/recipe-list.component.ts`

**Step 1: Add template section**

```html
<div class="import-bar">
  <input type="url" [(ngModel)]="importUrl" placeholder="Paste recipe URL..." />
  <button (click)="importFromUrl()" [disabled]="importing" class="btn btn-outline">
    {{ importing ? 'Importing...' : 'Import from URL' }}
  </button>
</div>
<div *ngIf="importError" class="error">{{ importError }}</div>
```

**Step 2: Add component logic**

```ts
import { RecipeImportService } from '../../services/recipe-import.service';
import { RecipeDraftService } from '../../services/recipe-draft.service';
import { Router } from '@angular/router';

importUrl = '';
importing = false;
importError = '';

constructor(
  private recipeService: RecipeService,
  private recipeImportService: RecipeImportService,
  private recipeDraftService: RecipeDraftService,
  private router: Router
) {}

importFromUrl() {
  if (!this.importUrl.trim()) {
    this.importError = 'Please enter a URL.';
    return;
  }
  this.importing = true;
  this.importError = '';
  this.recipeImportService.importFromUrl(this.importUrl.trim()).subscribe({
    next: (draft) => {
      this.recipeDraftService.setDraft(draft);
      this.importing = false;
      this.router.navigate(['/recipes/new']);
    },
    error: () => {
      this.importError = 'Failed to import recipe from URL.';
      this.importing = false;
    }
  });
}
```

**Step 3: Add minimal styles in component styles block**

```css
.import-bar { display: flex; gap: 0.5rem; margin-bottom: 1rem; }
.import-bar input { flex: 1; padding: 0.5rem; min-height: 44px; }
```

**Step 4: Commit**

```bash
git add frontend/src/app/pages/recipe-list/recipe-list.component.ts
git commit -m "feat: add URL import UI on recipe list"
```

---

### Task 6: Pre-fill Recipe Editor from draft

**Files:**
- Modify: `frontend/src/app/pages/recipe-editor/recipe-editor.component.ts`

**Step 1: Inject draft service and load draft**

```ts
import { RecipeDraftService } from '../../services/recipe-draft.service';
import { RecipeDraft } from '../../models/recipe.model';

private draftWarnings: string[] = [];

constructor(
  private route: ActivatedRoute,
  private router: Router,
  private recipeService: RecipeService,
  private recipeDraftService: RecipeDraftService
) {}

ngOnInit() {
  // existing edit/new logic
  if (!this.isEdit) {
    const draft = this.recipeDraftService.consumeDraft();
    if (draft) {
      this.applyDraft(draft);
    } else {
      this.addIngredient();
      this.addStep();
    }
  }
}

applyDraft(draft: RecipeDraft) {
  this.recipe.title = draft.title || '';
  this.recipe.description = draft.description || '';
  this.recipe.servings = draft.servings;
  this.recipe.prepMinutes = draft.prepMinutes;
  this.recipe.cookMinutes = draft.cookMinutes;
  this.ingredients = draft.ingredients?.length ? draft.ingredients : [{ name: '', quantity: '', unit: '', notes: '' }];
  this.steps = draft.steps?.length ? draft.steps : [{ instruction: '', timerSeconds: undefined }];
  this.tagsInput = (draft.tags || []).join(', ');
  this.draftWarnings = draft.warnings || [];
}
```

**Step 2: Add warnings display in template**

```html
<div *ngIf="draftWarnings.length" class="warning">
  <div *ngFor="let warning of draftWarnings">{{ warning }}</div>
</div>
```

**Step 3: Add warning style**

```css
.warning { padding: 0.75rem; background: #fff3cd; color: #856404; border-radius: 4px; margin-bottom: 1rem; }
```

**Step 4: Commit**

```bash
git add frontend/src/app/pages/recipe-editor/recipe-editor.component.ts
git commit -m "feat: prefill recipe editor from import draft"
```

---

### Task 7: Verification

**Step 1: Run backend tests**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
Expected: PASS

**Step 2: Run frontend tests**

Run: `npm test -- --watch=false`
Expected: PASS (2 tests)

**Step 3: Manual check**
- Start backend + frontend.
- On Recipe List page, paste URL to a known recipe with JSON-LD.
- Confirm editor opens with title, ingredients, steps pre-filled.

**Step 4: Commit any test-related fixes**

```bash
git add backend/src/RecipeManager.Api/Controllers/RecipeController.cs backend/src/RecipeManager.Api/Services/RecipeImportService.cs frontend/src/app/pages/recipe-list/recipe-list.component.ts frontend/src/app/pages/recipe-editor/recipe-editor.component.ts
git commit -m "test: verify URL import flow"
```
