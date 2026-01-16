using Google.Apis.Auth;
using Identity.Application.Interfaces;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Auth;

public class GoogleAuthService : IGoogleAuthService
{
    private readonly GoogleAuthSettings _googleSettings;

    public GoogleAuthService(IOptions<GoogleAuthSettings> googleSettings)
    {
        _googleSettings = googleSettings.Value;
    }

    public async Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string idToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { _googleSettings.ClientId }
            };

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);

            if (payload == null)
                return null;

            return new GoogleUserInfo(
                Email: payload.Email,
                FirstName: payload.GivenName,
                LastName: payload.FamilyName,
                Picture: payload.Picture,
                EmailVerified: payload.EmailVerified
            );
        }
        catch
        {
            return null;
        }
    }
}