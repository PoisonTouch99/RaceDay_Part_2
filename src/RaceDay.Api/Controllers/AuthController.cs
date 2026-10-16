using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Registration, login and logout. Uses server-side session state.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;

    public AuthController(RaceDayDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>Register a new account.</summary>
    /// <remarks>
    /// Creates an account as either an **Organiser** or a **Participant**. The role is chosen
    /// at registration and fixed thereafter, which is what drives all role-based access in the API.
    /// The password is hashed with PBKDF2 before storage and is never persisted in plain text.
    /// </remarks>
    /// <response code="201">Account created. Returns the new user (without the password).</response>
    /// <response code="400">Validation failed, e.g. missing fields or an invalid role.</response>
    /// <response code="409">That email address is already registered.</response>
    [HttpPost("register")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            return Conflict(new { message = "An account with that email address already exists." });

        var user = new User
        {
            FullName = request.FullName.Trim(),
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            Role = request.Role,
            Phone = request.Phone,
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(Register), new { id = user.UserId }, UserResponse.From(user));
    }

    /// <summary>Log in and start a session.</summary>
    /// <remarks>
    /// On success the server stores the user's id and role in the session and returns a session
    /// cookie. Send that cookie on subsequent requests to access protected endpoints.
    /// In Swagger UI the cookie is stored by the browser automatically after calling this endpoint.
    /// </remarks>
    /// <response code="200">Logged in. Returns the authenticated user.</response>
    /// <response code="401">Email or password is incorrect.</response>
    [HttpPost("login")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == email);

        // Same message for unknown email and wrong password, so the response
        // doesn't reveal which accounts exist.
        if (user is null || !_hasher.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "Invalid email or password." });

        HttpContext.SignIn(user);
        return Ok(UserResponse.From(user));
    }

    /// <summary>Log out and clear the session.</summary>
    /// <response code="204">Session cleared.</response>
    /// <response code="401">No active session.</response>
    [HttpPost("logout")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Logout()
    {
        HttpContext.SignOut();
        return NoContent();
    }

    /// <summary>Returns the currently logged-in user, or 401 if there is no session.</summary>
    /// <remarks>Useful for confirming that session authentication is working.</remarks>
    /// <response code="200">The current session user.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Me()
    {
        var user = await _db.Users.FindAsync(HttpContext.RequireUserId());
        return user is null ? Unauthorized() : Ok(UserResponse.From(user));
    }
}
