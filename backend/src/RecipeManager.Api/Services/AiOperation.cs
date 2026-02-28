namespace RecipeManager.Api.Services;

public enum AiOperation
{
    RecipeImport = 1,
    ImageSelection = 2,
    ModelList = 3,
    PaperCardImport = 4,
    MealAssistant = 5,
    NutritionEstimate = 6
}

public static class AiOperationMapper
{
    public static string ToStorageValue(this AiOperation operation)
    {
        return operation switch
        {
            AiOperation.RecipeImport => "RecipeImport",
            AiOperation.ImageSelection => "ImageSelection",
            AiOperation.ModelList => "ModelList",
            AiOperation.PaperCardImport => "PaperCardImport",
            AiOperation.MealAssistant => "MealAssistant",
            AiOperation.NutritionEstimate => "NutritionEstimate",
            _ => operation.ToString()
        };
    }

    public static IReadOnlyList<string> KnownOperations { get; } = Enum
        .GetValues<AiOperation>()
        .Select(o => o.ToStorageValue())
        .Distinct(StringComparer.Ordinal)
        .OrderBy(o => o, StringComparer.Ordinal)
        .ToList();
}
