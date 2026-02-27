using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using HtmlAgilityPack;
using RecipeManager.Api.DTOs;
using RecipeManager.Api.Infrastructure.Storage;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Services;

public class RecipeImportService
{
    private static readonly Regex SizedFileSuffixRegex = new("-(\\d+)x(\\d+)(?=\\.[^.]+$)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AiRecipeImportService _aiImportService;
    private readonly ImageFetchService _imageFetchService;
    private readonly IStorageService _storageService;

    public RecipeImportService(
        IHttpClientFactory httpClientFactory,
        AiRecipeImportService aiImportService,
        ImageFetchService imageFetchService,
        IStorageService storageService)
    {
        _httpClientFactory = httpClientFactory;
        _aiImportService = aiImportService;
        _imageFetchService = imageFetchService;
        _storageService = storageService;
    }

    public async Task<RecipeDraftDto> ImportFromUrlAsync(string url, Household household, Guid? userId = null)
    {
        var client = _httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddBrowserHeaders(request);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        return await ExtractDraftFromHtmlAsync(html, url, household, userId);
    }

    private static void AddBrowserHeaders(HttpRequestMessage request)
    {
        request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
        request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
        request.Headers.Referrer = new Uri("https://www.google.com/");
    }

    public async Task<RecipeDraftDto> ExtractDraftFromHtmlAsync(string html, string url, Household household, Guid? userId = null)
    {
        RecipeDraftDto draft;

        var jsonLdDraft = TryParseJsonLd(html);
        if (jsonLdDraft != null)
        {
            draft = jsonLdDraft with { ConfidenceScore = 0.8 };
        }
        else if (HasAiSettings(household))
        {
            var readableText = ExtractReadableText(html);
            var wasTruncated = readableText.Length > 100_000;
            if (wasTruncated)
            {
                readableText = readableText[..100_000];
            }

            draft = await _aiImportService.ImportAsync(
                household.AiProvider!,
                household.AiModel!,
                household.AiApiKeyEncrypted!,
                url,
                readableText,
                wasTruncated,
                household.Id,
                userId);
        }
        else
        {
            var heuristicDraft = TryParseHeuristics(html);
            draft = heuristicDraft with
            {
                ConfidenceScore = 0.4,
                Warnings = new List<string> { "JSON-LD not found; used heuristic extraction." }
            };
        }

        draft = draft with { SourceUrl = url };

        if (HasAiSettings(household))
        {
            var imageResult = await TryImportImagesAsync(url, html, draft, household, userId);
            if (imageResult.ImportedImages.Count > 0 || imageResult.CandidateImages.Count > 0)
            {
                return draft with
                {
                    ImportedImages = imageResult.ImportedImages,
                    CandidateImages = imageResult.CandidateImages
                };
            }
        }

        return draft;
    }

    private static bool HasAiSettings(Household household)
    {
        return !string.IsNullOrWhiteSpace(household.AiProvider) &&
               !string.IsNullOrWhiteSpace(household.AiModel) &&
               !string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted);
    }

    public string ExtractReadableText(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var junkNodes = doc.DocumentNode.SelectNodes("//script|//style|//nav|//header|//footer|//noscript");
        if (junkNodes != null)
        {
            foreach (var node in junkNodes)
            {
                node.Remove();
            }
        }

        var segments = new List<string>();
        var main = doc.DocumentNode.SelectSingleNode("//main") ?? doc.DocumentNode;
        var mainText = NormalizeText(main.InnerText);
        if (!string.IsNullOrWhiteSpace(mainText))
        {
            segments.Add(mainText);
        }

        var ingredientRows = doc.DocumentNode.SelectNodes(
            "//*[contains(translate(@class,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'),'ingredients-table')]//tr");
        if (ingredientRows != null)
        {
            foreach (var row in ingredientRows)
            {
                var cells = row.SelectNodes("./th|./td|.//th|.//td");
                if (cells == null || cells.Count == 0) continue;

                var rowText = string.Join(" ",
                    cells.Select(c => NormalizeText(c.InnerText))
                        .Where(t => !string.IsNullOrWhiteSpace(t)));

                if (!string.IsNullOrWhiteSpace(rowText))
                {
                    segments.Add(rowText);
                }
            }
        }

        return string.Join('\n', segments.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string NormalizeText(string text)
    {
        var decoded = HtmlEntity.DeEntitize(text);
        return string.Join(' ',
            decoded.Split('\n', '\r', '\t')
                .Select(t => t.Trim())
                .Where(t => t.Length > 0));
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

                var recipe = recipeElement.Value;
                var title = recipe.TryGetProperty("name", out var name) ? name.GetString() : null;
                if (string.IsNullOrWhiteSpace(title)) title = "Imported Recipe";

                var description = recipe.TryGetProperty("description", out var desc) ? desc.GetString() : null;
                var ingredients = ParseIngredients(recipe);
                var steps = ParseSteps(recipe);
                var tags = ParseTags(recipe);

                return new RecipeDraftDto(
                    title,
                    description,
                    TryParseServings(recipe),
                    TryParseMinutes(recipe, "prepTime"),
                    TryParseMinutes(recipe, "cookTime"),
                    ingredients,
                    steps,
                    tags,
                    new List<ImportedImageDto>(),
                    new List<CandidateImageDto>(),
                    null,
                    new List<string>(),
                    null
                );
            }
            catch
            {
                // Try next JSON-LD block
            }
        }

        return null;
    }

    private static List<IngredientDto> ParseIngredients(JsonElement recipe)
    {
        var ingredients = new List<IngredientDto>();
        if (!recipe.TryGetProperty("recipeIngredient", out var ing)) return ingredients;

        if (ing.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ing.EnumerateArray())
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ingredients.Add(new IngredientDto(text, null, null, null));
                }
            }
        }
        else if (ing.ValueKind == JsonValueKind.String)
        {
            var text = ing.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                ingredients.Add(new IngredientDto(text, null, null, null));
            }
        }

        return ingredients;
    }

    private static List<StepDto> ParseSteps(JsonElement recipe)
    {
        var steps = new List<StepDto>();
        if (!recipe.TryGetProperty("recipeInstructions", out var instr)) return steps;

        if (instr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in instr.EnumerateArray())
            {
                var text = ExtractInstructionText(item);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    steps.Add(new StepDto(text, null));
                }
            }
        }
        else
        {
            var text = ExtractInstructionText(instr);
            if (!string.IsNullOrWhiteSpace(text))
            {
                steps.Add(new StepDto(text, null));
            }
        }

        return steps;
    }

    private static string? ExtractInstructionText(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    private static List<string> ParseTags(JsonElement recipe)
    {
        if (!recipe.TryGetProperty("keywords", out var keywords)) return new List<string>();

        var text = keywords.GetString();
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        return text.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => t.Length > 0)
            .ToList();
    }

    private static JsonElement? FindRecipeElement(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (IsRecipeType(root)) return root;

            if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in graph.EnumerateArray())
                {
                    if (IsRecipeType(item)) return item;
                }
            }
        }
        else if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                if (IsRecipeType(item)) return item;
            }
        }

        return null;
    }

    private static bool IsRecipeType(JsonElement element)
    {
        if (!element.TryGetProperty("@type", out var type)) return false;
        return type.ValueKind == JsonValueKind.String && type.GetString() == "Recipe";
    }

    private static RecipeDraftDto TryParseHeuristics(string html)
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
            new List<ImportedImageDto>(),
            new List<CandidateImageDto>(),
            null,
            new List<string>(),
            null
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

    public List<string> ExtractImageCandidates(string html, Uri baseUri)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var candidates = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']") ?? Enumerable.Empty<HtmlNode>())
        {
            try
            {
                using var parsed = JsonDocument.Parse(node.InnerText);
                var recipeElement = FindRecipeElement(parsed.RootElement);
                if (recipeElement != null && recipeElement.Value.TryGetProperty("image", out var imageEl))
                {
                    CollectImageCandidates(imageEl, candidates, seen, baseUri);
                }
            }
            catch
            {
                // ignore invalid JSON-LD
            }
        }

        var ogImage = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
        var ogContent = ogImage?.GetAttributeValue("content", null);
        AddCandidate(ogContent, candidates, seen, baseUri);

        var containers = doc.DocumentNode.SelectNodes("//main|//article");
        if (containers != null)
        {
            foreach (var container in containers)
            {
                foreach (var img in container.SelectNodes(".//img") ?? Enumerable.Empty<HtmlNode>())
                {
                    AddImageCandidatesFromNode(img, candidates, seen, baseUri);
                }
            }
        }

        return SelectBestCandidates(candidates);
    }

    private static void CollectImageCandidates(JsonElement imageEl, List<string> candidates, HashSet<string> seen, Uri baseUri)
    {
        if (imageEl.ValueKind == JsonValueKind.String)
        {
            AddCandidate(imageEl.GetString(), candidates, seen, baseUri);
            return;
        }

        if (imageEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in imageEl.EnumerateArray())
            {
                CollectImageCandidates(item, candidates, seen, baseUri);
            }
            return;
        }

        if (imageEl.ValueKind == JsonValueKind.Object)
        {
            if (imageEl.TryGetProperty("url", out var urlEl))
            {
                CollectImageCandidates(urlEl, candidates, seen, baseUri);
            }
        }
    }

    private static void AddCandidate(string? url, List<string> candidates, HashSet<string> seen, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return;

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            var value = absolute.ToString();
            if (seen.Add(value))
            {
                candidates.Add(value);
            }
            return;
        }

        if (Uri.TryCreate(baseUri, url, out var resolved))
        {
            var value = resolved.ToString();
            if (seen.Add(value))
            {
                candidates.Add(value);
            }
        }
    }

    private static void AddImageCandidatesFromNode(HtmlNode img, List<string> candidates, HashSet<string> seen, Uri baseUri)
    {
        // Common lazy-loading attributes used by WP and similar CMS setups.
        var directAttributes = new[]
        {
            "src",
            "data-src",
            "data-lazy-src",
            "data-jpibfi-src",
            "data-original"
        };

        foreach (var attribute in directAttributes)
        {
            AddCandidate(img.GetAttributeValue(attribute, null), candidates, seen, baseUri);
        }

        var srcSetAttributes = new[] { "srcset", "data-srcset", "data-lazy-srcset" };
        foreach (var attribute in srcSetAttributes)
        {
            AddBestCandidateFromSrcSet(img.GetAttributeValue(attribute, null), candidates, seen, baseUri);
        }
    }

    private static void AddBestCandidateFromSrcSet(string? srcSet, List<string> candidates, HashSet<string> seen, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(srcSet)) return;

        string? bestUrl = null;
        var bestWidth = -1;

        foreach (var part in srcSet.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (trimmed.Length == 0) continue;

            // srcset item format: "<url> <descriptor>" (descriptor optional)
            var firstSpace = trimmed.IndexOf(' ');
            var url = firstSpace > 0 ? trimmed[..firstSpace] : trimmed;
            var descriptor = firstSpace > 0 ? trimmed[(firstSpace + 1)..].Trim() : string.Empty;
            var width = TryParseWidthDescriptor(descriptor);
            if (width > bestWidth)
            {
                bestWidth = width;
                bestUrl = url;
            }
        }

        AddCandidate(bestUrl, candidates, seen, baseUri);
    }

    private static int TryParseWidthDescriptor(string descriptor)
    {
        // Prefer numeric width descriptors like "731w". Unknown descriptors are ranked lowest.
        if (string.IsNullOrWhiteSpace(descriptor)) return 0;
        if (descriptor.EndsWith("w", StringComparison.OrdinalIgnoreCase))
        {
            var numeric = descriptor[..^1];
            if (int.TryParse(numeric, out var width))
            {
                return width;
            }
        }

        return 0;
    }

    private static List<string> SelectBestCandidates(List<string> candidates)
    {
        var bestByKey = new Dictionary<string, (string Url, int Score, int FirstIndex)>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < candidates.Count; i++)
        {
            var url = candidates[i];
            var key = BuildImageIdentityKey(url);
            var score = ScoreImageQuality(url);

            if (!bestByKey.TryGetValue(key, out var existing))
            {
                bestByKey[key] = (url, score, i);
                continue;
            }

            if (score > existing.Score)
            {
                // Keep the original list position stable while upgrading to the better quality variant.
                bestByKey[key] = (url, score, existing.FirstIndex);
            }
        }

        return bestByKey.Values
            .OrderBy(v => v.FirstIndex)
            .Select(v => v.Url)
            .ToList();
    }

    private static string BuildImageIdentityKey(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url;
        }

        var path = uri.AbsolutePath;
        var directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileName(path);
        var normalizedName = SizedFileSuffixRegex.Replace(fileName, string.Empty);
        return $"{uri.Scheme}://{uri.Host}{directory}/{normalizedName}".ToLowerInvariant();
    }

    private static int ScoreImageQuality(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return 0;
        }

        var fileName = Path.GetFileName(uri.AbsolutePath);
        var sizedMatch = SizedFileSuffixRegex.Match(fileName);
        if (sizedMatch.Success &&
            int.TryParse(sizedMatch.Groups[1].Value, out var width) &&
            int.TryParse(sizedMatch.Groups[2].Value, out var height))
        {
            return width * height;
        }

        return 0;
    }

    private async Task<ImageImportResult> TryImportImagesAsync(
        string url,
        string html,
        RecipeDraftDto draft,
        Household household,
        Guid? userId = null)
    {
        try
        {
            var baseUri = new Uri(url);
            var candidates = ExtractImageCandidates(html, baseUri);
            if (candidates.Count == 0) return new ImageImportResult();

            var fetched = await _imageFetchService.FetchImagesAsync(candidates, maxImages: 20, pageUri: baseUri);
            if (fetched.Count == 0) return new ImageImportResult();

            AiImageSelection? selection = null;
            if (HasAiSettings(household))
            {
                try
                {
                    selection = await _aiImportService.SelectImagesAsync(
                        household.AiProvider!,
                        household.AiModel!,
                        household.AiApiKeyEncrypted!,
                        draft,
                        fetched,
                        household.Id,
                        userId);
                }
                catch
                {
                    selection = null;
                }
            }

            var selected = SelectImagesFromResult(selection, fetched);
            var candidatesStored = await StoreCandidateImagesAsync(selected, fetched);
            return new ImageImportResult(new List<ImportedImageDto>(), candidatesStored);
        }
        catch
        {
            return new ImageImportResult();
        }
    }

    private static List<SelectedImage> SelectImagesFromResult(
        AiImageSelection? selection,
        List<FetchedImage> fetched)
    {
        var selected = new List<SelectedImage>();
        var used = new HashSet<int>();

        if (selection?.HeroIndex is >= 0 && selection.HeroIndex < fetched.Count)
        {
            used.Add(selection.HeroIndex.Value);
            selected.Add(new SelectedImage(fetched[selection.HeroIndex.Value], true, 0));
        }
        else if (fetched.Count > 0)
        {
            used.Add(0);
            selected.Add(new SelectedImage(fetched[0], true, 0));
        }

        var stepImages = selection?.StepImages ?? new List<AiStepSelection>();
        var orderedSteps = stepImages
            .Where(s => s.Index >= 0 && s.Index < fetched.Count && !used.Contains(s.Index))
            .OrderBy(s => s.StepIndex ?? int.MaxValue)
            .ThenBy(s => s.Index)
            .Take(20)
            .ToList();

        var orderIndex = 1;
        foreach (var step in orderedSteps)
        {
            used.Add(step.Index);
            selected.Add(new SelectedImage(fetched[step.Index], false, orderIndex++));
        }

        return selected;
    }

    private async Task<List<ImportedImageDto>> StoreSelectedImagesAsync(List<SelectedImage> selected)
    {
        var result = new List<ImportedImageDto>();
        foreach (var image in selected)
        {
            var fileName = BuildFileName(image.Image.Url, image.Image.ContentType);
            await using var stream = new MemoryStream(image.Image.Bytes);
            var storedUrl = await _storageService.UploadAsync(stream, fileName, image.Image.ContentType);
            result.Add(new ImportedImageDto(storedUrl, image.IsTitleImage, image.OrderIndex));
        }

        return result;
    }

    private async Task<List<CandidateImageDto>> StoreCandidateImagesAsync(
        List<SelectedImage> selected,
        List<FetchedImage> fetched)
    {
        var selectedUrlSet = selected.Select(s => s.Image.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var selectedOrderMap = selected.ToDictionary(s => s.Image.Url, s => s.OrderIndex, StringComparer.OrdinalIgnoreCase);
        var result = new List<CandidateImageDto>();
        var order = 0;

        foreach (var image in fetched)
        {
            var fileName = BuildTempFileName(image.Url, image.ContentType);
            await using var stream = new MemoryStream(image.Bytes);
            var storedUrl = await _storageService.UploadAsync(stream, fileName, image.ContentType);
            result.Add(new CandidateImageDto(
                storedUrl,
                selectedUrlSet.Contains(image.Url),
                selectedOrderMap.TryGetValue(image.Url, out var selectedOrder) ? selectedOrder : order));
            order++;
        }

        return result;
    }

    private static string BuildFileName(string url, string contentType)
    {
        var fileName = Path.GetFileName(new Uri(url).AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = "imported";
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = contentType switch
            {
                "image/png" => ".png",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                _ => ".jpg"
            };
            fileName += extension;
        }

        return fileName;
    }

    private static string BuildTempFileName(string url, string contentType)
    {
        var fileName = BuildFileName(url, contentType);
        return $"temp_{fileName}";
    }

    private record SelectedImage(FetchedImage Image, bool IsTitleImage, int OrderIndex);

    private record ImageImportResult(
        List<ImportedImageDto> ImportedImages,
        List<CandidateImageDto> CandidateImages)
    {
        public ImageImportResult() : this(new List<ImportedImageDto>(), new List<CandidateImageDto>()) { }
    }
}
