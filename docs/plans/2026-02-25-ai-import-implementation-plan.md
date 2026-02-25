# AI URL Import Fallback Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Add AI-based URL import fallback with OpenAI/Anthropic using per-household encrypted settings, plus a Household Settings page to manage provider/model/key.

**Architecture:** Extend `Household` with encrypted AI settings, add owner-only settings endpoints, add AI client services, and update `RecipeImportService` decision tree (JSON-LD → AI if configured → heuristic). Frontend adds a new Household Settings page with provider/model/key inputs and model list fetch.

**Tech Stack:** ASP.NET Core, EF Core, Data Protection, Angular standalone components, HttpClient, RxJS.

---

### Task 1: Add household AI settings fields + migration

**Files:**
- Modify: `backend/src/RecipeManager.Api/Models/Household.cs`
- Modify: `backend/src/RecipeManager.Api/Data/AppDbContext.cs`
- Create: `backend/src/RecipeManager.Api/Migrations/<timestamp>_AddHouseholdAiSettings.cs`
- Modify: `backend/src/RecipeManager.Api/Migrations/AppDbContextModelSnapshot.cs`

**Step 1: Add fields to Household**

```csharp
public string? AiProvider { get; set; }
public string? AiModel { get; set; }
public string? AiApiKeyEncrypted { get; set; }
```

**Step 2: Create EF migration**

Run: `dotnet ef migrations add AddHouseholdAiSettings -s backend/src/RecipeManager.Api`
Expected: new migration files with three nullable columns.

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Models/Household.cs backend/src/RecipeManager.Api/Migrations/*AddHouseholdAiSettings* backend/src/RecipeManager.Api/Migrations/AppDbContextModelSnapshot.cs
git commit -m "feat: add household AI settings fields"
```

---

### Task 2: Add AI settings DTOs + encryption service

**Files:**
- Create: `backend/src/RecipeManager.Api/DTOs/HouseholdSettingsDtos.cs`
- Create: `backend/src/RecipeManager.Api/Services/HouseholdAiSettingsService.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs`

**Step 1: DTOs**

```csharp
public record HouseholdAiSettingsDto(
    string? AiProvider,
    string? AiModel,
    bool HasApiKey
);

public record UpdateHouseholdAiSettingsRequest(
    string? AiProvider,
    string? AiModel,
    string? ApiKey
);
```

**Step 2: Encryption service (Data Protection)**

```csharp
public class HouseholdAiSettingsService
{
    private readonly IDataProtector _protector;

    public HouseholdAiSettingsService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HouseholdAiSettings");
    }

    public string Encrypt(string apiKey) => _protector.Protect(apiKey);
    public string Decrypt(string encrypted) => _protector.Unprotect(encrypted);
}
```

**Step 3: Register Data Protection + service**

```csharp
builder.Services.AddDataProtection();
builder.Services.AddSingleton<HouseholdAiSettingsService>();
```

**Step 4: Commit**

```bash
git add backend/src/RecipeManager.Api/DTOs/HouseholdSettingsDtos.cs backend/src/RecipeManager.Api/Services/HouseholdAiSettingsService.cs backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add household AI settings DTOs and encryption service"
```

---

### Task 3: Add Household settings endpoints (owner-only)

**Files:**
- Modify: `backend/src/RecipeManager.Api/Controllers/HouseholdController.cs`

**Step 1: Add GET/PUT endpoints**

```csharp
[HttpGet("settings")]
public async Task<ActionResult<HouseholdAiSettingsDto>> GetAiSettings()
{
    var (householdId, role) = await GetMembershipOrFail();
    if (role != "Owner") return Forbid();

    var household = await _db.Households.FindAsync(householdId);
    if (household == null) return NotFound();

    return Ok(new HouseholdAiSettingsDto(
        household.AiProvider,
        household.AiModel,
        !string.IsNullOrEmpty(household.AiApiKeyEncrypted)
    ));
}

[HttpPut("settings")]
public async Task<ActionResult<HouseholdAiSettingsDto>> UpdateAiSettings(
    [FromBody] UpdateHouseholdAiSettingsRequest request,
    [FromServices] HouseholdAiSettingsService aiSettings)
{
    var (householdId, role) = await GetMembershipOrFail();
    if (role != "Owner") return Forbid();

    var household = await _db.Households.FindAsync(householdId);
    if (household == null) return NotFound();

    household.AiProvider = request.AiProvider;
    household.AiModel = request.AiModel;
    if (!string.IsNullOrWhiteSpace(request.ApiKey))
    {
        household.AiApiKeyEncrypted = aiSettings.Encrypt(request.ApiKey);
    }

    await _db.SaveChangesAsync();

    return Ok(new HouseholdAiSettingsDto(
        household.AiProvider,
        household.AiModel,
        !string.IsNullOrEmpty(household.AiApiKeyEncrypted)
    ));
}
```

**Step 2: Add helper to HouseholdController**

```csharp
private async Task<(Guid householdId, string role)> GetMembershipOrFail()
{
    var userId = GetUserId() ?? throw new UnauthorizedAccessException();
    var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(hm => hm.UserId == userId);
    if (membership == null) throw new InvalidOperationException("User has no household");
    return (membership.HouseholdId, membership.Role);
}
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Controllers/HouseholdController.cs
git commit -m "feat: add household AI settings endpoints"
```

---

### Task 4: Add AI provider model list endpoint

**Files:**
- Create: `backend/src/RecipeManager.Api/Controllers/AiController.cs`
- Create: `backend/src/RecipeManager.Api/Services/AiModelCatalogService.cs`

**Step 1: AiModelCatalogService**

```csharp
public class AiModelCatalogService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;

    public AiModelCatalogService(IHttpClientFactory factory, HouseholdAiSettingsService settings)
    {
        _httpClientFactory = factory;
        _settings = settings;
    }

    public async Task<List<string>> GetModelsAsync(string provider, string encryptedKey)
    {
        var apiKey = _settings.Decrypt(encryptedKey);
        var client = _httpClientFactory.CreateClient();

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var json = await client.GetStringAsync("https://api.openai.com/v1/models");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(m => m.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .OrderBy(id => id)
                .ToList();
        }

        if (provider == "Anthropic")
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var json = await client.GetStringAsync("https://api.anthropic.com/v1/models");
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("data")
                .EnumerateArray()
                .Select(m => m.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .OrderBy(id => id)
                .ToList();
        }

        return new List<string>();
    }
}
```

**Step 2: AiController**

```csharp
[ApiController]
[Route("api/ai")]
[Authorize]
public class AiController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly AiModelCatalogService _catalog;

    public AiController(AppDbContext db, AiModelCatalogService catalog)
    {
        _db = db;
        _catalog = catalog;
    }

    [HttpGet("providers/models")]
    public async Task<ActionResult<List<string>>> GetModels([FromQuery] string provider)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userId, out var uid)) return Unauthorized();

        var membership = await _db.HouseholdMembers.FirstOrDefaultAsync(h => h.UserId == uid);
        if (membership == null) return BadRequest("User does not belong to a household");
        if (membership.Role != "Owner") return Forbid();

        var household = await _db.Households.FindAsync(membership.HouseholdId);
        if (household == null) return NotFound();
        if (string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted)) return BadRequest("API key not set");

        var models = await _catalog.GetModelsAsync(provider, household.AiApiKeyEncrypted);
        return Ok(models);
    }
}
```

**Step 3: Register service**

```csharp
builder.Services.AddScoped<AiModelCatalogService>();
```

**Step 4: Commit**

```bash
git add backend/src/RecipeManager.Api/Controllers/AiController.cs backend/src/RecipeManager.Api/Services/AiModelCatalogService.cs backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add AI model catalog endpoint"
```

---

### Task 5: Add AI client + import fallback logic

**Files:**
- Create: `backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs`
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`

**Step 1: AiRecipeImportService**

```csharp
public class AiRecipeImportService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;

    public AiRecipeImportService(IHttpClientFactory factory, HouseholdAiSettingsService settings)
    {
        _httpClientFactory = factory;
        _settings = settings;
    }

    public async Task<RecipeDraftDto> ImportAsync(string provider, string model, string encryptedKey, string url)
    {
        var apiKey = _settings.Decrypt(encryptedKey);
        var client = _httpClientFactory.CreateClient();

        var prompt = $"Extract a recipe from this URL and return JSON with fields: title, description, servings, prepMinutes, cookMinutes, ingredients (name, quantity, unit, notes), steps (instruction, timerSeconds), tags. URL: {url}";

        if (provider == "OpenAI")
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
            var payload = new
            {
                model,
                messages = new[] { new { role = "user", content = prompt } },
                response_format = new { type = "json_object" }
            };
            var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            return ParseDraftFromJson(content);
        }

        if (provider == "Anthropic")
        {
            client.DefaultRequestHeaders.Add("x-api-key", apiKey);
            client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            var payload = new
            {
                model,
                max_tokens = 2048,
                messages = new[] { new { role = "user", content = prompt } }
            };
            var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            var content = json.GetProperty("content")[0].GetProperty("text").GetString();
            return ParseDraftFromJson(content);
        }

        throw new InvalidOperationException("Unsupported provider");
    }

    private static RecipeDraftDto ParseDraftFromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidOperationException("Empty AI response");
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var ingredients = root.GetProperty("ingredients").EnumerateArray()
            .Select(i => new IngredientDto(
                i.GetProperty("name").GetString() ?? string.Empty,
                i.TryGetProperty("quantity", out var q) ? q.GetString() : null,
                i.TryGetProperty("unit", out var u) ? u.GetString() : null,
                i.TryGetProperty("notes", out var n) ? n.GetString() : null
            )).ToList();

        var steps = root.GetProperty("steps").EnumerateArray()
            .Select(s => new StepDto(
                s.GetProperty("instruction").GetString() ?? string.Empty,
                s.TryGetProperty("timerSeconds", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : null
            )).ToList();

        var tags = root.TryGetProperty("tags", out var tagEl) && tagEl.ValueKind == JsonValueKind.Array
            ? tagEl.EnumerateArray().Select(t => t.GetString() ?? string.Empty).Where(t => t.Length > 0).ToList()
            : new List<string>();

        return new RecipeDraftDto(
            root.GetProperty("title").GetString() ?? "Imported Recipe",
            root.TryGetProperty("description", out var desc) ? desc.GetString() : null,
            root.TryGetProperty("servings", out var serv) && serv.ValueKind == JsonValueKind.Number ? serv.GetInt32() : null,
            root.TryGetProperty("prepMinutes", out var prep) && prep.ValueKind == JsonValueKind.Number ? prep.GetInt32() : null,
            root.TryGetProperty("cookMinutes", out var cook) && cook.ValueKind == JsonValueKind.Number ? cook.GetInt32() : null,
            ingredients,
            steps,
            tags,
            0.6,
            new List<string> { "Imported with AI" }
        );
    }
}
```

**Step 2: Update RecipeImportService decision tree**

```csharp
public async Task<RecipeDraftDto> ImportFromUrlAsync(string url, Household household)
{
    var html = await client.GetStringAsync(url);
    var jsonLdDraft = TryParseJsonLd(html);
    if (jsonLdDraft != null) return jsonLdDraft with { ConfidenceScore = 0.8 };

    if (!string.IsNullOrWhiteSpace(household.AiProvider) &&
        !string.IsNullOrWhiteSpace(household.AiModel) &&
        !string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted))
    {
        return await _aiImport.ImportAsync(household.AiProvider, household.AiModel, household.AiApiKeyEncrypted, url);
    }

    return TryParseHeuristics(html) with { ConfidenceScore = 0.4, Warnings = new List<string> { "JSON-LD not found; used heuristic extraction." } };
}
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs backend/src/RecipeManager.Api/Services/RecipeImportService.cs backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add AI import fallback"
```

---

### Task 6: Update import endpoint to use household settings

**Files:**
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`

**Step 1: Load household and pass to import service**

```csharp
var household = await _db.Households.FindAsync(membership.Value.householdId);
if (household == null) return NotFound();

var draft = await importService.ImportFromUrlAsync(request.Url, household);
```

**Step 2: Commit**

```bash
git add backend/src/RecipeManager.Api/Controllers/RecipeController.cs
git commit -m "feat: use household AI settings for import"
```

---

### Task 7: Add Household Settings page + API wiring (frontend)

**Files:**
- Create: `frontend/src/app/pages/household-settings/household-settings.component.ts`
- Modify: `frontend/src/app/app.routes.ts`
- Create: `frontend/src/app/services/household-settings.service.ts`
- Modify: `frontend/src/app/services/auth.service.ts`

**Step 1: Service**

```ts
export interface HouseholdAiSettings {
  aiProvider?: string;
  aiModel?: string;
  hasApiKey: boolean;
}

export interface UpdateHouseholdAiSettingsRequest {
  aiProvider?: string;
  aiModel?: string;
  apiKey?: string;
}
```

Add `getSettings()`, `updateSettings()`, `getModels(provider)` methods.

**Step 2: Component**
- Provider dropdown (OpenAI/Anthropic)
- Model dropdown (loaded by calling `getModels`)
- API key field (masked)
- Save disabled until models loaded
- Owner-only: redirect non-owners

**Step 3: Routes**

```ts
{ path: 'household/settings', canActivate: [authGuard], loadComponent: () => import('./pages/household-settings/household-settings.component').then(m => m.HouseholdSettingsComponent) }
```

**Step 4: Commit**

```bash
git add frontend/src/app/pages/household-settings/household-settings.component.ts frontend/src/app/app.routes.ts frontend/src/app/services/household-settings.service.ts
git commit -m "feat: add household AI settings page"
```

---

### Task 8: Verification

**Step 1: Run backend tests**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
Expected: PASS

**Step 2: Run frontend tests**

Run: `npm test -- --watch=false`
Expected: PASS

**Step 3: Manual check**
- Set provider + API key + model as Owner.
- Import URL without JSON-LD.
- Ensure AI fallback used (draft warning shows AI).

**Step 4: Commit any fixes**

```bash
git add backend/src/RecipeManager.Api/Controllers/RecipeController.cs backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs frontend/src/app/pages/household-settings/household-settings.component.ts
git commit -m "test: verify AI import flow"
```
