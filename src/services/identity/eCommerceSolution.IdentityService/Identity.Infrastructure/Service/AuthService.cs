using Identity.Application.DTOs.Auth;
using Identity.Application.Interfaces;
using Identity.Domain.Common;
using Identity.Domain.Entities;
using Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IGoogleAuthService _googleAuthService;
    private readonly JwtSettings _jwtSettings;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        IJwtTokenGenerator jwtTokenGenerator,
        IGoogleAuthService googleAuthService,
        IOptions<JwtSettings> jwtSettings)
    {
        _userManager = userManager;
        _jwtTokenGenerator = jwtTokenGenerator;
        _googleAuthService = googleAuthService;
        _jwtSettings = jwtSettings.Value;
    }

    public async Task<Result<AuthResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        // Check if user exists
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return Result<AuthResponse>.Failure(
                "User already exists",
                new List<string> { "A user with this email already exists" }
            );
        }

        // Create new user
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            UserName = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PhoneNumber = request.PhoneNumber,
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return Result<AuthResponse>.Failure(
                "Registration failed",
                result.Errors.Select(e => e.Description).ToList()
            );
        }

        // Assign default role
        await _userManager.AddToRoleAsync(user, "Customer");

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        // Save refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
        await _userManager.UpdateAsync(user);

        var response = new AuthResponse(
            UserId: user.Id.ToString(),
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );

        return Result<AuthResponse>.Success(response, "Registration successful");
    }

    public async Task<Result<AuthResponse>> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Find user
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null || !user.IsActive)
        {
            return Result<AuthResponse>.Failure(
                "Invalid credentials",
                new List<string> { "Email or password is incorrect" }
            );
        }

        // Check password using UserManager directly
        var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);

        if (!isPasswordValid)
        {
            // Handle failed login attempt
            await _userManager.AccessFailedAsync(user);

            // Check if user is locked out
            if (await _userManager.IsLockedOutAsync(user))
            {
                return Result<AuthResponse>.Failure(
                    "Account locked",
                    new List<string> { "Your account has been locked due to multiple failed login attempts. Please try again later." }
                );
            }

            return Result<AuthResponse>.Failure(
                "Invalid credentials",
                new List<string> { "Email or password is incorrect" }
            );
        }

        // Reset access failed count on successful login
        await _userManager.ResetAccessFailedCountAsync(user);

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        // Save refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
        await _userManager.UpdateAsync(user);

        var response = new AuthResponse(
            UserId: user.Id.ToString(),
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );

        return Result<AuthResponse>.Success(response, "Login successful");
    }

    public async Task<Result<AuthResponse>> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var users = _userManager.Users.Where(u =>
            u.RefreshToken == request.RefreshToken &&
            u.RefreshTokenExpiry > DateTime.UtcNow
        );

        var user = users.FirstOrDefault();

        if (user == null)
        {
            return Result<AuthResponse>.Failure(
                "Invalid refresh token",
                new List<string> { "The refresh token is invalid or expired" }
            );
        }

        // Generate new tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        // Update refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
        await _userManager.UpdateAsync(user);

        var response = new AuthResponse(
            UserId: user.Id.ToString(),
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );

        return Result<AuthResponse>.Success(response, "Token refreshed successfully");
    }

    public async Task<Result<AuthResponse>> GoogleLoginAsync(
        GoogleLoginRequest request,
        CancellationToken cancellationToken = default)
    {
        // Validate Google token
        var googleUser = await _googleAuthService.ValidateGoogleTokenAsync(request.IdToken);

        if (googleUser == null)
        {
            return Result<AuthResponse>.Failure(
                "Invalid Google token",
                new List<string> { "The Google token is invalid or expired" }
            );
        }

        // Check if user exists
        var user = await _userManager.FindByEmailAsync(googleUser.Email);

        if (user == null)
        {
            // Create new user
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                Email = googleUser.Email,
                UserName = googleUser.Email,
                FirstName = googleUser.FirstName,
                LastName = googleUser.LastName,
                EmailConfirmed = googleUser.EmailVerified,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            var createResult = await _userManager.CreateAsync(user);

            if (!createResult.Succeeded)
            {
                return Result<AuthResponse>.Failure(
                    "Registration failed",
                    createResult.Errors.Select(e => e.Description).ToList()
                );
            }

            // Assign default role
            await _userManager.AddToRoleAsync(user, "Customer");

            // Add Google login info
            await _userManager.AddLoginAsync(user, new UserLoginInfo("Google", googleUser.Email, "Google"));
        }

        // Generate tokens
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = _jwtTokenGenerator.GenerateAccessToken(user, roles);
        var refreshToken = _jwtTokenGenerator.GenerateRefreshToken();

        // Save refresh token
        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
        await _userManager.UpdateAsync(user);

        var response = new AuthResponse(
            UserId: user.Id.ToString(),
            Email: user.Email!,
            FirstName: user.FirstName,
            LastName: user.LastName,
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpirationMinutes)
        );

        return Result<AuthResponse>.Success(response, "Google login successful");
    }

    public async Task<Result<bool>> LogoutAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await _userManager.FindByIdAsync(userId);

        if (user == null)
        {
            return Result<bool>.Failure("User not found");
        }

        // Clear refresh token
        user.RefreshToken = null;
        user.RefreshTokenExpiry = null;
        await _userManager.UpdateAsync(user);

        return Result<bool>.Success(true, "Logout successful");
    }
}