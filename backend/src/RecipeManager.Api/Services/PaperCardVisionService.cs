using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using RecipeManager.Api.Models;
using RecipeManager.Api.DTOs;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace RecipeManager.Api.Services;

public record PaperCardVisionResult(
    string Title,
    string? Description,
    Dictionary<int, List<IngredientDto>> IngredientsByServings,
    List<StepDto> Steps,
    string? RawFrontText,
    string? RawBackText,
    double? ConfidenceScore,
    List<string> Warnings,
    ImageRegionDto? HeroImageRegion,
    List<ImageRegionDto> StepImageRegions
);

public record ImageRegionDto(
    double X,
    double Y,
    double Width,
    double Height,
    int RotationDegrees
);

public interface IPaperCardVisionService
{
    Task<PaperCardVisionResult> ExtractAsync(
        IFormFile frontImage,
        IFormFile backImage,
        Household household,
        Guid? userId,
        CancellationToken cancellationToken);
}

public class PaperCardVisionService : IPaperCardVisionService
{
    private const int AnthropicMaxImageBytes = 4_500_000;
    private const int AnthropicMaxDimension = 1800;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly HouseholdAiSettingsService _settings;
    private readonly AiDebugLogService? _debugLogService;
    private readonly ILogger<PaperCardVisionService> _logger;

    public PaperCardVisionService(
        IHttpClientFactory httpClientFactory,
        HouseholdAiSettingsService settings,
        AiDebugLogService? debugLogService,
        ILogger<PaperCardVisionService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _settings = settings;
        _debugLogService = debugLogService;
        _logger = logger;
    }

    public PaperCardVisionService(
        IHttpClientFactory httpClientFactory,
        HouseholdAiSettingsService settings,
        ILogger<PaperCardVisionService> logger)
        : this(httpClientFactory, settings, null, logger)
    {
    }

    public async Task<PaperCardVisionResult> ExtractAsync(
        IFormFile frontImage,
        IFormFile backImage,
        Household household,
        Guid? userId,
        CancellationToken cancellationToken)
    {
        var fallbackTitle = BuildTitleFromFileName(frontImage.FileName);

        if (string.IsNullOrWhiteSpace(household.AiProvider) ||
            string.IsNullOrWhiteSpace(household.AiModel) ||
            string.IsNullOrWhiteSpace(household.AiApiKeyEncrypted))
        {
            return BuildFallback(fallbackTitle, "Household AI is not configured. Configure AI settings to enable OCR parsing.");
        }

        try
        {
            var provider = household.AiProvider!;
            var model = household.AiModel!;
            var apiKey = _settings.Decrypt(household.AiApiKeyEncrypted!).Trim();
            var client = _httpClientFactory.CreateClient();

            var forAnthropic = provider == "Anthropic";
            var (frontBytes, frontContentType) = await PrepareImageForAiAsync(frontImage, forAnthropic, cancellationToken);
            var (backBytes, backContentType) = await PrepareImageForAiAsync(backImage, forAnthropic, cancellationToken);
            var prompt = BuildPrompt();

            string payloadJson;
            string responseBody;
            int statusCode;
            string? contentText;

            if (provider == "OpenAI")
            {
                client.DefaultRequestHeaders.Authorization = new("Bearer", apiKey);
                var payload = BuildOpenAiPayload(model, prompt, frontContentType, frontBytes, backContentType, backBytes);
                payloadJson = JsonSerializer.Serialize(payload);
                var response = await client.PostAsJsonAsync("https://api.openai.com/v1/chat/completions", payload, cancellationToken);
                statusCode = (int)response.StatusCode;
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    await LogDebugAsync(household.Id, userId, provider, model, payloadJson, responseBody, statusCode, false, "OpenAI paper-card parse failed");
                    return BuildFallback(fallbackTitle, $"AI parsing failed with status {statusCode}. You can still edit manually.");
                }

                using var doc = JsonDocument.Parse(responseBody);
                contentText = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString();
            }
            else if (provider == "Anthropic")
            {
                client.DefaultRequestHeaders.Add("x-api-key", apiKey);
                client.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
                var payload = BuildAnthropicPayload(model, prompt, frontContentType, frontBytes, backContentType, backBytes);
                payloadJson = JsonSerializer.Serialize(payload);
                var response = await client.PostAsJsonAsync("https://api.anthropic.com/v1/messages", payload, cancellationToken);
                statusCode = (int)response.StatusCode;
                responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (!response.IsSuccessStatusCode)
                {
                    await LogDebugAsync(household.Id, userId, provider, model, payloadJson, responseBody, statusCode, false, "Anthropic paper-card parse failed");
                    return BuildFallback(fallbackTitle, $"AI parsing failed with status {statusCode}. You can still edit manually.");
                }

                using var doc = JsonDocument.Parse(responseBody);
                contentText = doc.RootElement.GetProperty("content")[0].GetProperty("text").GetString();
            }
            else
            {
                return BuildFallback(fallbackTitle, $"Unsupported AI provider '{provider}'.");
            }

            await LogDebugAsync(household.Id, userId, household.AiProvider!, household.AiModel!, payloadJson, responseBody, statusCode, true, null);
            var parsed = ParseResult(contentText, fallbackTitle);
            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Paper card AI parse failed");
            return BuildFallback(fallbackTitle, "AI parsing failed unexpectedly. You can still complete the recipe manually.");
        }
    }

    private static object BuildOpenAiPayload(string model, string prompt, string frontContentType, byte[] frontBytes, string backContentType, byte[] backBytes)
    {
        return new
        {
            model,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "text", text = "Front side image (title + hero image):" },
                        new { type = "image_url", image_url = new { url = $"data:{frontContentType};base64,{Convert.ToBase64String(frontBytes)}" } },
                        new { type = "text", text = "Back side image (ingredient table + cooking steps + step photos):" },
                        new { type = "image_url", image_url = new { url = $"data:{backContentType};base64,{Convert.ToBase64String(backBytes)}" } }
                    }
                }
            },
            response_format = new { type = "json_object" }
        };
    }

    private static object BuildAnthropicPayload(string model, string prompt, string frontContentType, byte[] frontBytes, string backContentType, byte[] backBytes)
    {
        return new
        {
            model,
            max_tokens = 2500,
            messages = new object[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "text", text = "Front side image (title + hero image):" },
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = frontContentType,
                                data = Convert.ToBase64String(frontBytes)
                            }
                        },
                        new { type = "text", text = "Back side image (ingredient table + cooking steps + step photos):" },
                        new
                        {
                            type = "image",
                            source = new
                            {
                                type = "base64",
                                media_type = backContentType,
                                data = Convert.ToBase64String(backBytes)
                            }
                        }
                    }
                }
            }
        };
    }

    private static async Task<(byte[] Bytes, string ContentType)> PrepareImageForAiAsync(
        IFormFile file,
        bool forAnthropic,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var image = await Image.LoadAsync(stream, cancellationToken);
        image.Metadata.ExifProfile = null;

        if (forAnthropic)
        {
            return await PrepareAnthropicImageAsync(image, cancellationToken);
        }

        await using var ms = new MemoryStream();
        var contentType = file.ContentType.ToLowerInvariant();
        switch (contentType)
        {
            case "image/png":
                await image.SaveAsPngAsync(ms, new PngEncoder(), cancellationToken);
                return (ms.ToArray(), "image/png");
            case "image/webp":
                await image.SaveAsWebpAsync(ms, new WebpEncoder(), cancellationToken);
                return (ms.ToArray(), "image/webp");
            default:
                await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 92 }, cancellationToken);
                return (ms.ToArray(), "image/jpeg");
        }
    }

    private static async Task<(byte[] Bytes, string ContentType)> PrepareAnthropicImageAsync(
        Image image,
        CancellationToken cancellationToken)
    {
        using var working = image.CloneAs<SixLabors.ImageSharp.PixelFormats.Rgba32>();
        DownscaleIfNeeded(working, AnthropicMaxDimension);

        foreach (var quality in new[] { 90, 80, 70, 60, 50, 40 })
        {
            var data = await EncodeJpegAsync(working, quality, cancellationToken);
            if (data.Length <= AnthropicMaxImageBytes)
            {
                return (data, "image/jpeg");
            }
        }

        while (working.Width > 900 && working.Height > 900)
        {
            var nextWidth = (int)Math.Round(working.Width * 0.85);
            var nextHeight = (int)Math.Round(working.Height * 0.85);
            working.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Size = new Size(nextWidth, nextHeight),
                Mode = ResizeMode.Max
            }));

            var data = await EncodeJpegAsync(working, 40, cancellationToken);
            if (data.Length <= AnthropicMaxImageBytes)
            {
                return (data, "image/jpeg");
            }
        }

        var finalData = await EncodeJpegAsync(working, 35, cancellationToken);
        return (finalData, "image/jpeg");
    }

    private static void DownscaleIfNeeded(Image image, int maxDimension)
    {
        if (image.Width <= maxDimension && image.Height <= maxDimension)
        {
            return;
        }

        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(maxDimension, maxDimension),
            Mode = ResizeMode.Max
        }));
    }

    private static async Task<byte[]> EncodeJpegAsync(Image image, int quality, CancellationToken cancellationToken)
    {
        await using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder
        {
            Quality = quality,
            Interleaved = true
        }, cancellationToken);
        return ms.ToArray();
    }

    private static string BuildPrompt()
    {
        return
            "You are parsing a HelloFresh paper recipe card from two images: front and back.\n" +
            "Extract structured recipe data and return JSON only with this exact schema:\n" +
            "{\n" +
            "  \"title\": string,\n" +
            "  \"description\": string|null,\n" +
            "  \"ingredientsByServings\": {\n" +
            "    \"2\": [ { \"name\": string, \"quantity\": string|null, \"unit\": string|null, \"notes\": string|null } ],\n" +
            "    \"3\": [ ... ],\n" +
            "    \"4\": [ ... ]\n" +
            "  },\n" +
            "  \"steps\": [ { \"instruction\": string, \"timerSeconds\": number|null } ],\n" +
            "  \"heroImageRegion\": { \"x\": number, \"y\": number, \"width\": number, \"height\": number, \"rotationDegrees\": 0|90|180|270 }|null,\n" +
            "  \"stepImageRegions\": [ { \"x\": number, \"y\": number, \"width\": number, \"height\": number, \"rotationDegrees\": 0|90|180|270 } ],\n" +
            "  \"warnings\": [string],\n" +
            "  \"confidenceScore\": number\n" +
            "}\n" +
            "Rules:\n" +
            "- The first image is front side and the second image is back side.\n" +
            "- Keep ingredient names exactly as printed where possible.\n" +
            "- If a serving section is missing, return an empty array for that serving key.\n" +
            "- Extract ingredients from the back-side ingredient table for 2/3/4 servings.\n" +
            "- heroImageRegion uses normalized coordinates in range [0,1] relative to front image.\n" +
            "- stepImageRegions uses normalized coordinates in range [0,1] relative to back image.\n" +
            "- Use rotationDegrees so cropped images are upright. For heroImageRegion, rotate so dish/title reads with top at top.\n" +
            "- Include every individual step photo on the back side in cooking order.\n" +
            "- Do not include markdown fences.\n";
    }

    private static PaperCardVisionResult ParseResult(string? content, string fallbackTitle)
    {
        var sanitized = SanitizeJson(content);
        using var doc = JsonDocument.Parse(sanitized);
        var root = doc.RootElement;

        var title = root.TryGetProperty("title", out var titleEl) && titleEl.ValueKind == JsonValueKind.String
            ? titleEl.GetString() ?? fallbackTitle
            : fallbackTitle;

        var description = root.TryGetProperty("description", out var descEl) && descEl.ValueKind == JsonValueKind.String
            ? descEl.GetString()
            : null;

        var ingredientsByServings = new Dictionary<int, List<IngredientDto>>
        {
            [2] = new(),
            [3] = new(),
            [4] = new()
        };
        if (root.TryGetProperty("ingredientsByServings", out var servingsEl) && servingsEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var serving in new[] { 2, 3, 4 })
            {
                if (servingsEl.TryGetProperty(serving.ToString(), out var ingredientArray) && ingredientArray.ValueKind == JsonValueKind.Array)
                {
                    ingredientsByServings[serving] = ingredientArray.EnumerateArray()
                        .Select(item => new IngredientDto(
                            item.TryGetProperty("name", out var name) ? name.GetString() ?? string.Empty : string.Empty,
                            item.TryGetProperty("quantity", out var quantity) && quantity.ValueKind != JsonValueKind.Null ? quantity.GetString() : null,
                            item.TryGetProperty("unit", out var unit) && unit.ValueKind != JsonValueKind.Null ? unit.GetString() : null,
                            item.TryGetProperty("notes", out var notes) && notes.ValueKind != JsonValueKind.Null ? notes.GetString() : null
                        ))
                        .Where(i => !string.IsNullOrWhiteSpace(i.Name))
                        .ToList();
                }
            }
        }

        var steps = new List<StepDto>();
        if (root.TryGetProperty("steps", out var stepsEl) && stepsEl.ValueKind == JsonValueKind.Array)
        {
            steps = stepsEl.EnumerateArray()
                .Select(item =>
                {
                    var instruction = item.TryGetProperty("instruction", out var ins) ? ins.GetString() ?? string.Empty : string.Empty;
                    int? timer = null;
                    if (item.TryGetProperty("timerSeconds", out var timerEl) && timerEl.ValueKind == JsonValueKind.Number && timerEl.TryGetInt32(out var timerValue))
                    {
                        timer = timerValue;
                    }
                    return new StepDto(instruction, timer);
                })
                .Where(s => !string.IsNullOrWhiteSpace(s.Instruction))
                .ToList();
        }

        var warnings = new List<string>();
        if (root.TryGetProperty("warnings", out var warningsEl) && warningsEl.ValueKind == JsonValueKind.Array)
        {
            warnings = warningsEl.EnumerateArray()
                .Where(w => w.ValueKind == JsonValueKind.String)
                .Select(w => w.GetString()!)
                .Where(w => !string.IsNullOrWhiteSpace(w))
                .ToList();
        }

        var heroRegion = TryParseRegion(root, "heroImageRegion");
        var stepRegions = new List<ImageRegionDto>();
        if (root.TryGetProperty("stepImageRegions", out var stepRegionsEl) && stepRegionsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in stepRegionsEl.EnumerateArray())
            {
                var parsed = TryParseRegion(el);
                if (parsed != null)
                {
                    stepRegions.Add(parsed);
                }
            }
        }

        double? confidence = null;
        if (root.TryGetProperty("confidenceScore", out var confidenceEl) && confidenceEl.ValueKind == JsonValueKind.Number && confidenceEl.TryGetDouble(out var score))
        {
            confidence = score;
        }

        return new PaperCardVisionResult(
            title,
            description,
            ingredientsByServings,
            steps,
            null,
            null,
            confidence ?? 0.55,
            warnings,
            heroRegion,
            stepRegions
        );
    }

    private static ImageRegionDto? TryParseRegion(JsonElement owner, string propertyName)
    {
        if (!owner.TryGetProperty(propertyName, out var regionEl))
        {
            return null;
        }

        return TryParseRegion(regionEl);
    }

    private static ImageRegionDto? TryParseRegion(JsonElement regionEl)
    {
        if (regionEl.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!TryGetDouble(regionEl, "x", out var x) ||
            !TryGetDouble(regionEl, "y", out var y) ||
            !TryGetDouble(regionEl, "width", out var width) ||
            !TryGetDouble(regionEl, "height", out var height))
        {
            return null;
        }

        var rotation = 0;
        if (regionEl.TryGetProperty("rotationDegrees", out var rotEl) &&
            rotEl.ValueKind == JsonValueKind.Number &&
            rotEl.TryGetInt32(out var parsedRotation))
        {
            rotation = parsedRotation;
        }

        return new ImageRegionDto(x, y, width, height, rotation);
    }

    private static bool TryGetDouble(JsonElement owner, string propertyName, out double value)
    {
        value = 0;
        if (!owner.TryGetProperty(propertyName, out var el))
        {
            return false;
        }

        return el.ValueKind == JsonValueKind.Number && el.TryGetDouble(out value);
    }

    private static string SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "{}";
        }

        var trimmed = json.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var startFenceEnd = trimmed.IndexOf('\n');
        if (startFenceEnd < 0)
        {
            return trimmed;
        }

        var endFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (endFence <= startFenceEnd)
        {
            return trimmed;
        }

        return trimmed.Substring(startFenceEnd + 1, endFence - startFenceEnd - 1).Trim();
    }

    private async Task LogDebugAsync(
        Guid householdId,
        Guid? userId,
        string provider,
        string model,
        string? requestJson,
        string? responseJson,
        int? statusCode,
        bool success,
        string? error)
    {
        if (_debugLogService == null)
        {
            return;
        }

        await _debugLogService.LogAsync(
            householdId,
            userId,
            provider,
            model,
            AiOperation.PaperCardImport,
            requestJson,
            responseJson,
            statusCode,
            success,
            error);
    }

    private static PaperCardVisionResult BuildFallback(string title, string warning)
    {
        var ingredientsByServings = new Dictionary<int, List<IngredientDto>>
        {
            [2] = new List<IngredientDto>(),
            [3] = new List<IngredientDto>(),
            [4] = new List<IngredientDto>()
        };

        var steps = new List<StepDto>
        {
            new("Review extracted cooking steps from card back image and adjust as needed.", null)
        };

        var warnings = new List<string>
        {
            warning,
            "Paper card OCR extraction is running in guided fallback mode. Please review title, ingredients, and steps."
        };

        return new PaperCardVisionResult(
            title,
            null,
            ingredientsByServings,
            steps,
            null,
            null,
            0.25,
            warnings,
            null,
            new List<ImageRegionDto>()
        );

    }

    private static string BuildTitleFromFileName(string fileName)
    {
        var withoutExt = Path.GetFileNameWithoutExtension(fileName);
        if (string.IsNullOrWhiteSpace(withoutExt))
        {
            return "Imported Paper Card Recipe";
        }

        var normalized = Regex.Replace(withoutExt, "[-_]+", " ").Trim();
        normalized = Regex.Replace(normalized, "\\s+", " ");
        if (normalized.Length == 0)
        {
            return "Imported Paper Card Recipe";
        }

        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }
}
