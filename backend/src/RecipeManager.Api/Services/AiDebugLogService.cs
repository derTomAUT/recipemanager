using System.Text.Json;
using System.Text.Json.Nodes;
using RecipeManager.Api.Data;
using RecipeManager.Api.Models;

namespace RecipeManager.Api.Services;

public class AiDebugLogService
{
    private const int MaxPayloadLength = 30_000;
    private static readonly string[] SensitiveKeys =
    {
        "authorization", "api_key", "apikey", "x-api-key", "token", "secret", "password", "key"
    };

    private readonly AppDbContext _db;
    private readonly ILogger<AiDebugLogService> _logger;

    public AiDebugLogService(AppDbContext db, ILogger<AiDebugLogService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task LogAsync(
        Guid? householdId,
        Guid? userId,
        string provider,
        string model,
        string operation,
        string? requestJson,
        string? responseJson,
        int? statusCode,
        bool success,
        string? error)
    {
        try
        {
            var entry = new AiDebugLog
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                HouseholdId = householdId,
                UserId = userId,
                Provider = provider,
                Model = model,
                Operation = operation,
                RequestJsonSanitized = SanitizePayload(requestJson),
                ResponseJsonSanitized = SanitizePayload(responseJson),
                StatusCode = statusCode,
                Success = success,
                Error = Truncate(error)
            };

            _db.AiDebugLogs.Add(entry);
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist AI debug log");
        }
    }

    public Task LogAsync(
        Guid? householdId,
        Guid? userId,
        string provider,
        string model,
        AiOperation operation,
        string? requestJson,
        string? responseJson,
        int? statusCode,
        bool success,
        string? error)
    {
        return LogAsync(
            householdId,
            userId,
            provider,
            model,
            operation.ToStorageValue(),
            requestJson,
            responseJson,
            statusCode,
            success,
            error);
    }

    private static string SanitizePayload(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return string.Empty;
        }

        try
        {
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return Truncate(json);
            }
            SanitizeNode(node, keyName: null);
            return Truncate(node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        }
        catch
        {
            return Truncate(RedactRaw(json));
        }
    }

    private static void SanitizeNode(JsonNode node, string? keyName)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (property.Key == null) continue;
                if (IsSensitiveKey(property.Key))
                {
                    obj[property.Key] = "[REDACTED]";
                    continue;
                }

                if (property.Value != null)
                {
                    SanitizeNode(property.Value, property.Key);
                }
            }
            return;
        }

        if (node is JsonArray arr)
        {
            for (var i = 0; i < arr.Count; i++)
            {
                if (arr[i] != null)
                {
                    SanitizeNode(arr[i]!, keyName);
                }
            }
            return;
        }

        if (node is JsonValue value)
        {
            if (!value.TryGetValue<string>(out var stringValue) || stringValue == null) return;

            if (stringValue.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
            {
                ReplaceValue(node, "[REDACTED_IMAGE_DATA]");
                return;
            }

            if (keyName != null && keyName.Equals("data", StringComparison.OrdinalIgnoreCase) && stringValue.Length > 200)
            {
                ReplaceValue(node, "[REDACTED_BASE64]");
                return;
            }

            if (stringValue.Length > 4_000)
            {
                ReplaceValue(node, $"{stringValue[..4_000]}...[TRUNCATED]");
            }
        }
    }

    private static void ReplaceValue(JsonNode node, string replacement)
    {
        if (node.Parent is JsonObject parentObj)
        {
            var kv = parentObj.FirstOrDefault(p => p.Value == node);
            if (kv.Key != null)
            {
                parentObj[kv.Key] = replacement;
            }
            return;
        }

        if (node.Parent is JsonArray parentArr)
        {
            var idx = parentArr.IndexOf(node);
            if (idx >= 0)
            {
                parentArr[idx] = replacement;
            }
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        var lowered = key.ToLowerInvariant();
        return SensitiveKeys.Any(token => lowered.Contains(token, StringComparison.Ordinal));
    }

    private static string RedactRaw(string value)
    {
        var redacted = value.Replace("Bearer ", "Bearer [REDACTED]", StringComparison.OrdinalIgnoreCase);
        return redacted;
    }

    private static string Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= MaxPayloadLength
            ? value
            : $"{value[..MaxPayloadLength]}...[TRUNCATED]";
    }
}
