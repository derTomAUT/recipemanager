using System.Text.Json;

namespace RecipeManager.Api.Services;

public sealed record NutritionMacroSnapshot(
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    decimal? Fiber,
    decimal? Sugar,
    decimal? SodiumMg
);

public sealed record NutritionEstimateSnapshot(
    NutritionMacroSnapshot PerServing,
    NutritionMacroSnapshot Total,
    string? Notes
);

public static class NutritionEstimateParser
{
    public static NutritionEstimateSnapshot Parse(string? aiContent)
    {
        var json = AiResponseParser.ExtractJsonObjectText(aiContent);
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("AI response did not contain a JSON nutrition object.");
        }

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("perServing", out var perServingEl) || perServingEl.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Nutrition response missing perServing object.");
        }

        if (!root.TryGetProperty("total", out var totalEl) || totalEl.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Nutrition response missing total object.");
        }

        var notes = root.TryGetProperty("notes", out var notesEl) && notesEl.ValueKind == JsonValueKind.String
            ? notesEl.GetString()
            : null;

        return new NutritionEstimateSnapshot(
            ParseSnapshot(perServingEl),
            ParseSnapshot(totalEl),
            string.IsNullOrWhiteSpace(notes) ? null : notes.Trim());
    }

    private static NutritionMacroSnapshot ParseSnapshot(JsonElement element)
    {
        return new NutritionMacroSnapshot(
            ReadRequiredNumber(element, "calories"),
            ReadRequiredNumber(element, "protein"),
            ReadRequiredNumber(element, "carbs"),
            ReadRequiredNumber(element, "fat"),
            ReadOptionalNumber(element, "fiber"),
            ReadOptionalNumber(element, "sugar"),
            ReadOptionalNumber(element, "sodiumMg"));
    }

    private static decimal ReadRequiredNumber(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
        {
            throw new InvalidOperationException($"Nutrition response missing required field '{name}'.");
        }

        return ReadDecimal(value, name);
    }

    private static decimal? ReadOptionalNumber(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return ReadDecimal(value, name);
    }

    private static decimal ReadDecimal(JsonElement value, string fieldName)
    {
        var parsed = value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var n) => n,
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var s) => s,
            _ => throw new InvalidOperationException($"Nutrition field '{fieldName}' is not a number.")
        };

        if (parsed < 0)
        {
            throw new InvalidOperationException($"Nutrition field '{fieldName}' cannot be negative.");
        }

        return Math.Round(parsed, 2, MidpointRounding.AwayFromZero);
    }
}
