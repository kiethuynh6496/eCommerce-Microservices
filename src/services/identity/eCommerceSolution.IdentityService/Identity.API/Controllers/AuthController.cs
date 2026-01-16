using Identity.Application.DTOs.Auth;
using Identity.Application.Interfaces;
using Identity.Domain.Entities;
using Identity.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Identity.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

        var result = await _authService.RegisterAsync(request);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Registration failed for email: {Email}. Errors: {Errors}",
                request.Email, string.Join(", ", result.Errors));
            return BadRequest(new
            {
                message = result.Message,
                errors = result.Errors
            });
        }

        _logger.LogInformation("User registered successfully: {Email}", request.Email);
        return Ok(new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        _logger.LogInformation("Login attempt for email: {Email}", request.Email);

        var result = await _authService.LoginAsync(request);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Login failed for email: {Email}", request.Email);
            return Unauthorized(new
            {
                message = result.Message,
                errors = result.Errors
            });
        }

        _logger.LogInformation("User logged in successfully: {Email}", request.Email);
        return Ok(new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        _logger.LogInformation("Refresh token attempt");

        var result = await _authService.RefreshTokenAsync(request);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Refresh token failed");
            return BadRequest(new
            {
                message = result.Message,
                errors = result.Errors
            });
        }

        _logger.LogInformation("Token refreshed successfully");
        return Ok(new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Login with Google OAuth
    /// </summary>
    [HttpPost("google-login")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        _logger.LogInformation("Google login attempt");

        var result = await _authService.GoogleLoginAsync(request);

        if (!result.IsSuccess)
        {
            _logger.LogWarning("Google login failed");
            return BadRequest(new
            {
                message = result.Message,
                errors = result.Errors
            });
        }

        _logger.LogInformation("Google login successful");
        return Ok(new
        {
            message = result.Message,
            data = result.Data
        });
    }

    /// <summary>
    /// Initiate Google OAuth flow (redirect to Google)
    /// </summary>
    [HttpGet("google-signin")]
    public IActionResult GoogleSignIn([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(
            nameof(GoogleCallback),
            "Auth",
            new { returnUrl },
            Request.Scheme
        );

        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Google OAuth callback - Returns tokens as JSON (for API testing without frontend)
    /// </summary>
    [HttpGet("google-callback")]
    [ApiExplorerSettings(IgnoreApi = false)]
    public async Task<IActionResult> GoogleCallback([FromQuery] string? returnUrl = null)
    {
        _logger.LogInformation("Google OAuth callback received");

        try
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(
                GoogleDefaults.AuthenticationScheme
            );

            if (!authenticateResult.Succeeded)
            {
                _logger.LogWarning("Google authentication failed");
                return BadRequest(new
                {
                    success = false,
                    message = "Google authentication failed",
                    error = "auth_failed"
                });
            }

            var email = authenticateResult.Principal?.FindFirst(c => c.Type == "email")?.Value;

            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Email not provided by Google",
                    error = "no_email"
                });
            }

            // Get ID token from authentication ticket
            var idToken = authenticateResult.Properties?.GetTokenValue("id_token");

            if (string.IsNullOrEmpty(idToken))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "ID token not found",
                    error = "no_token"
                });
            }

            var result = await _authService.GoogleLoginAsync(
                new GoogleLoginRequest(idToken)
            );

            if (!result.IsSuccess)
            {
                _logger.LogError("Google login failed: {Message}", result.Message);
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    errors = result.Errors
                });
            }

            var authResponse = result.Data!;

            return Ok(new
            {
                success = true,
                message = "Google login successful",
                data = new
                {
                    userId = authResponse.UserId,
                    email = authResponse.Email,
                    firstName = authResponse.FirstName,
                    lastName = authResponse.LastName,
                    accessToken = authResponse.AccessToken,
                    refreshToken = authResponse.RefreshToken,
                    expiresAt = authResponse.ExpiresAt
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Google callback");
            return StatusCode(500, new
            {
                success = false,
                message = "Internal server error",
                error = ex.Message
            });
        }
    }

    /// <summary>
    /// Logout
    /// </summary>
    [Authorize]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst("uid")?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new { message = "Invalid user" });
        }

        _logger.LogInformation("Logout attempt for user: {UserId}", userId);

        var result = await _authService.LogoutAsync(userId);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        _logger.LogInformation("User logged out successfully: {UserId}", userId);
        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Get current user
    /// </summary>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult GetCurrentUser()
    {
        var userId = User.FindFirst("uid")?.Value;
        var email = User.FindFirst(ClaimTypes.Email)?.Value;
        var firstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
        var lastName = User.FindFirst(ClaimTypes.Surname)?.Value;
        var roles = User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        return Ok(new
        {
            userId,
            email,
            firstName,
            lastName,
            roles
        });
    }
}