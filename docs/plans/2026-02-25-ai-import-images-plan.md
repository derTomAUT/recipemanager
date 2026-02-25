# AI Import Images Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Import a hero image and up to 20 step images when importing recipes from URLs, using AI vision on downloaded image bytes to select relevant images and step order.

**Architecture:** Extend URL import flow to collect and download candidate images, call AI vision (same provider/model) to classify hero + step images, then store selected images via `IStorageService` and attach to the new recipe.

**Tech Stack:** ASP.NET Core, HtmlAgilityPack, System.Text.Json, IHttpClientFactory, existing storage service.

---

### Task 1: Add image candidate extraction utilities

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`

**Step 1: Add helper to extract candidate image URLs**

Add methods:

```csharp
public List<string> ExtractImageCandidates(string html)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // JSON-LD image
    foreach (var node in doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
    {
        try
        {
            using var parsed = JsonDocument.Parse(node.InnerText);
            var recipe = FindRecipeElement(parsed.RootElement);
            if (recipe != null && recipe.Value.TryGetProperty("image", out var imageEl))
            {
                if (imageEl.ValueKind == JsonValueKind.String)
                {
                    candidates.Add(imageEl.GetString()!);
                }
                else if (imageEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in imageEl.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String) candidates.Add(item.GetString()!);
                    }
                }
            }
        }
        catch { }
    }

    // OpenGraph image
    var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
    var ogContent = ogImage?.GetAttributeValue("content", null);
    if (!string.IsNullOrWhiteSpace(ogContent)) candidates.Add(ogContent);

    // Images inside main/article
    var containers = doc.DocumentNode.SelectNodes("//main|//article") ?? new HtmlNodeCollection(null);
    foreach (var container in containers)
    {
        foreach (var img in container.SelectNodes(".//img") ?? Enumerable.Empty<HtmlNode>())
        {
            var src = img.GetAttributeValue("src", null);
            if (!string.IsNullOrWhiteSpace(src)) candidates.Add(src);
        }
    }

    return candidates.Where(u => Uri.TryCreate(u, UriKind.Absolute, out _)).ToList();
}
```

**Step 2: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs
git commit -m "feat: add image candidate extraction"
```

---

### Task 2: Download and filter image bytes

**Files:**
- Create: `backend/src/RecipeManager.Api/Services/ImageFetchService.cs`
- Modify: `backend/src/RecipeManager.Api/Program.cs`

**Step 1: Create ImageFetchService**

```csharp
public class ImageFetchService
{
    private readonly IHttpClientFactory _factory;

    public ImageFetchService(IHttpClientFactory factory)
    {
        _factory = factory;
    }

    public async Task<List<(string url, byte[] bytes, string contentType)>> FetchImagesAsync(IEnumerable<string> urls, int maxImages = 30, int maxBytes = 2_000_000)
    {
        var client = _factory.CreateClient();
        var results = new List<(string, byte[], string)>();

        foreach (var url in urls.Take(maxImages))
        {
            try
            {
                using var response = await client.GetAsync(url);
                if (!response.IsSuccessStatusCode) continue;

                var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
                if (!contentType.StartsWith("image/")) continue;

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0 || bytes.Length > maxBytes) continue;

                results.Add((url, bytes, contentType));
            }
            catch { }
        }

        return results;
    }
}
```

**Step 2: Register service**

```csharp
builder.Services.AddScoped<ImageFetchService>();
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/ImageFetchService.cs backend/src/RecipeManager.Api/Program.cs
git commit -m "feat: add image fetch service"
```

---

### Task 3: AI image classification for hero + steps

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs`

**Step 1: Add request model**

Define a new internal DTO for AI response:

```csharp
public record AiImageSelection(string? heroUrl, List<AiStepImage> stepImages);
public record AiStepImage(string url, int? stepIndex);
```

**Step 2: Add method to classify**

Add method `ClassifyImagesAsync` that sends a vision prompt with image bytes (base64) and returns selection.

Example prompt:

```
Given these images from a recipe page, identify:
1) The hero image showing the final cooked dish.
2) Up to 20 step images in cooking order (include stepIndex starting at 0 if known).
Return JSON: { heroUrl, stepImages: [{ url, stepIndex }] }
```

Include images using provider-specific vision formats (OpenAI: `image_url` base64; Anthropic: `image` blocks).

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs
git commit -m "feat: add AI image classification"
```

---

### Task 4: Persist selected images to recipe

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`
- Modify: `backend/src/RecipeManager.Api/Controllers/RecipeController.cs`

**Step 1: Extend import to store images**

If AI selection returns hero/step URLs:
- Download image bytes via `ImageFetchService` (reuse cached bytes if possible).
- Upload to storage using `IStorageService`.
- Create `RecipeImage` entries with `IsTitleImage=true` for hero and sequential `OrderIndex` for steps.

**Step 2: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs backend/src/RecipeManager.Api/Controllers/RecipeController.cs
git commit -m "feat: store AI-selected images on import"
```

---

### Task 5: Verification

**Step 1: Manual**
- Import a URL with multiple images, verify hero + step images saved.
- Confirm no crash when images are missing.

**Step 2: Commit any fixes**

```bash
git add backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs backend/src/RecipeManager.Api/Services/RecipeImportService.cs
git commit -m "test: verify image import"
```
