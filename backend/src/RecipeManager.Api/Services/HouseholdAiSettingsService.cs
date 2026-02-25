using Microsoft.AspNetCore.DataProtection;

namespace RecipeManager.Api.Services;

public class HouseholdAiSettingsService
{
    private readonly IDataProtector _protector;

    public HouseholdAiSettingsService(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("HouseholdAiSettings");
    }

    public string Encrypt(string apiKey) => _protector.Protect(apiKey);

    public string Decrypt(string encrypted) => _protector.Unprotect(encrypted);
}
