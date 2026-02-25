# AI Import Readable Text Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Include extracted readable text from recipe pages (up to 100 KB) in AI import prompts and log the JSON payload sent to AI providers.

**Architecture:** Extend `RecipeImportService` to extract readable text from HTML using HtmlAgilityPack and pass it to `AiRecipeImportService`. Update AI payload construction to include the extracted text and log the full JSON request payload at `Debug`.

**Tech Stack:** ASP.NET Core, HtmlAgilityPack, Serilog, System.Text.Json.

---

### Task 1: Add readable text extraction helper

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`
- Test: `backend/tests/RecipeManager.Api.Tests/RecipeImportServiceTests.cs`

**Step 1: Write a failing test for readable text extraction**

Add to `backend/tests/RecipeManager.Api.Tests/RecipeImportServiceTests.cs`:

```csharp
[Fact]
public void ExtractReadableText_StripsScriptAndReturnsContent()
{
    var html = @"<html><head><style>.x{}</style><script>ignore()</script></head>
<body><header>Header</header><main><h1>Title</h1><p>Keep this text.</p></main></body></html>";

    var service = new RecipeImportService(new TestHttpClientFactory(), new AiRecipeImportService(new TestHttpClientFactory(), new HouseholdAiSettingsService(DataProtectionProvider.Create("t")), new LoggerFactory().CreateLogger<AiRecipeImportService>()));
    var text = service.ExtractReadableText(html);

    Assert.Contains("Title", text);
    Assert.Contains("Keep this text.", text);
    Assert.DoesNotContain("ignore()", text);
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
Expected: FAIL due to missing `ExtractReadableText`.

**Step 3: Implement readable text extraction**

Add to `RecipeImportService`:

```csharp
public string ExtractReadableText(string html)
{
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    foreach (var node in doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//noscript") ?? Enumerable.Empty<HtmlNode>())
    {
        node.Remove();
    }

    var main = doc.DocumentNode.SelectSingleNode("//main") ?? doc.DocumentNode;
    var text = HtmlEntity.DeEntitize(main.InnerText);
    var cleaned = string.Join(' ', text.Split('\n', '\r', '\t').Select(t => t.Trim()).Where(t => t.Length > 0));
    return cleaned;
}
```

**Step 4: Run tests to verify pass**

Run: `dotnet test backend/tests/RecipeManager.Api.Tests/RecipeManager.Api.Tests.csproj`
Expected: PASS

**Step 5: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs backend/tests/RecipeManager.Api.Tests/RecipeImportServiceTests.cs
git commit -m "feat: add readable text extraction for AI import"
```

---

### Task 2: Pass readable text to AI import and cap at 100 KB

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/RecipeImportService.cs`
- Modify: `backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs`

**Step 1: Update import flow to pass text**

In `RecipeImportService.ExtractDraftFromHtmlAsync`, call `ExtractReadableText(html)` and pass to AI import when used. Truncate to 100 KB:

```csharp
var readableText = ExtractReadableText(html);
var truncated = readableText.Length > 100_000 ? readableText[..100_000] : readableText;
var wasTruncated = readableText.Length > 100_000;

return await _aiImportService.ImportAsync(
    household.AiProvider!,
    household.AiModel!,
    household.AiApiKeyEncrypted!,
    url,
    truncated,
    wasTruncated);
```

**Step 2: Update AI service signature and prompt**

Update `AiRecipeImportService.ImportAsync` signature to include `readableText` and `wasTruncated`, and include it in the prompt:

```csharp
var prompt = $"Extract a recipe from this URL. URL: {url}\n\n" +
             $"Readable text from the page (may be truncated={wasTruncated}):\n{readableText}\n\n" +
             "Return a single JSON object with fields: ...";
```

**Step 3: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs
git commit -m "feat: include readable text in AI import prompt"
```

---

### Task 3: Log AI JSON request payload

**Files:**
- Modify: `backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs`

**Step 1: Log JSON payload**

Before sending, serialize payload and log:

```csharp
var payloadJson = JsonSerializer.Serialize(payload);
_logger.LogDebug("AI import request (OpenAI): {Json}", payloadJson);
```

Repeat for Anthropic.

**Step 2: Commit**

```bash
git add backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs
git commit -m "feat: log AI request payload"
```

---

### Task 4: Verification

**Step 1: Manual import**
- Import a URL and confirm readable text appears in log payload.
- Verify truncated flag when text exceeds 100 KB.

**Step 2: Commit any fixes**

```bash
git add backend/src/RecipeManager.Api/Services/RecipeImportService.cs backend/src/RecipeManager.Api/Services/AiRecipeImportService.cs
git commit -m "test: verify AI import readable text"
```
