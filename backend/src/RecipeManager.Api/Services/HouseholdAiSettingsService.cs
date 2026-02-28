using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace RecipeManager.Api.Services;

public class HouseholdAiSettingsService
{
    private readonly IDataProtector _protector;

    public HouseholdAiSettingsService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HouseholdAiSettings");
    }

    public string Encrypt(string apiKey) => _protector.Protect(apiKey);

    public string Decrypt(string encrypted)
    {
        try
        {
            return _protector.Unprotect(encrypted);
        }
        catch (CryptographicException ex)
        {
            throw new AiKeyDecryptionException(
                "Stored AI API key can no longer be decrypted. Please re-save the API key in Household Settings.",
                ex);
        }
    }
}
