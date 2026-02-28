namespace RecipeManager.Api.Services;

public sealed class AiKeyDecryptionException : Exception
{
    public AiKeyDecryptionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
