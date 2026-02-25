using Google.Apis.Auth;

namespace RecipeManager.Api.Infrastructure.Auth;

public class GoogleTokenValidator
{
    private readonly string _googleClientId;

    public GoogleTokenValidator(IConfiguration config)
    {
        _googleClientId = config["Google:ClientId"]
            ?? throw new InvalidOperationException("Google ClientId not configured");
    }

    public async Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken)
    {
        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = new[] { _googleClientId }
        };
        return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
    }
}
