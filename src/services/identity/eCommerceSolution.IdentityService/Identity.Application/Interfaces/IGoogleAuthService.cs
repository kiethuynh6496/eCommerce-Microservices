namespace Identity.Application.Interfaces;

public interface IGoogleAuthService
{
    Task<GoogleUserInfo?> ValidateGoogleTokenAsync(string idToken);
}

public record GoogleUserInfo(
    string Email,
    string FirstName,
    string LastName,
    string? Picture,
    bool EmailVerified
);