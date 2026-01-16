using Identity.Domain.Entities;

namespace Identity.Application.Interfaces;

public interface IJwtTokenGenerator
{
    string GenerateAccessToken(ApplicationUser user, IList<string> roles);
    string GenerateRefreshToken();
    bool ValidateRefreshToken(string refreshToken);
}
