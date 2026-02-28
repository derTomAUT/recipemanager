using System.Text.Json;

namespace RecipeManager.Api.Services;

public static class AiResponseParser
{
    public static string? ExtractOpenAiMessageContent(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var message = doc.RootElement.GetProperty("choices")[0].GetProperty("message");
        return ExtractAssistantContent(message);
    }

    public static string? ExtractAnthropicMessageText(string responseBody)
    {
        using var doc = JsonDocument.Parse(responseBody);
        var contentEl = doc.RootElement.GetProperty("content");
        if (contentEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var item in contentEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                parts.Add(item.GetString() ?? string.Empty);
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                parts.Add(textEl.GetString() ?? string.Empty);
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static string? ExtractJsonObjectText(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var candidates = new List<string> { content.Trim() };
        if (TryExtractCodeFenceJson(content, out var codeFenceJson))
        {
            candidates.Add(codeFenceJson);
        }

        if (TryExtractFirstJsonObject(content, out var objectJson))
        {
            candidates.Add(objectJson);
        }

        foreach (var candidate in candidates.Distinct())
        {
            try
            {
                using var doc = JsonDocument.Parse(candidate);
                if (doc.RootElement.ValueKind == JsonValueKind.Object)
                {
                    return candidate;
                }
            }
            catch (JsonException)
            {
                // Try next candidate
            }
        }

        return null;
    }

    private static string? ExtractAssistantContent(JsonElement message)
    {
        if (!message.TryGetProperty("content", out var contentEl))
        {
            return null;
        }

        if (contentEl.ValueKind == JsonValueKind.String)
        {
            return contentEl.GetString();
        }

        if (contentEl.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var parts = new List<string>();
        foreach (var item in contentEl.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                parts.Add(item.GetString() ?? string.Empty);
                continue;
            }

            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (item.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String)
            {
                parts.Add(textEl.GetString() ?? string.Empty);
            }
        }

        return parts.Count == 0 ? null : string.Join("\n", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static bool TryExtractCodeFenceJson(string content, out string json)
    {
        var startFence = content.IndexOf("```", StringComparison.Ordinal);
        if (startFence < 0)
        {
            json = string.Empty;
            return false;
        }

        var firstLineEnd = content.IndexOf('\n', startFence);
        if (firstLineEnd < 0)
        {
            json = string.Empty;
            return false;
        }

        var endFence = content.IndexOf("```", firstLineEnd, StringComparison.Ordinal);
        if (endFence < 0)
        {
            json = string.Empty;
            return false;
        }

        json = content[(firstLineEnd + 1)..endFence].Trim();
        return json.Length > 0;
    }

    private static bool TryExtractFirstJsonObject(string text, out string json)
    {
        var start = text.IndexOf('{');
        if (start < 0)
        {
            json = string.Empty;
            return false;
        }

        var depth = 0;
        var inString = false;
        var escaped = false;
        for (var i = start; i < text.Length; i++)
        {
            var c = text[i];
            if (inString)
            {
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                }
                else if (c == '"')
                {
                    inString = false;
                }

                continue;
            }

            if (c == '"')
            {
                inString = true;
                continue;
            }

            if (c == '{')
            {
                depth++;
                continue;
            }

            if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    json = text[start..(i + 1)];
                    return true;
                }
            }
        }

        json = string.Empty;
        return false;
    }
}
