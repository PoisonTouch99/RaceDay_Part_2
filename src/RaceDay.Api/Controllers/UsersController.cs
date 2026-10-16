using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Services;

namespace RaceDay.Api.Controllers;

/// <summary>Profile management. Both roles manage only their own profile.</summary>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    private readonly IPasswordHasher _hasher;

    public UsersController(RaceDayDbContext db, IPasswordHasher hasher)
    {
        _db = db;
        _hasher = hasher;
    }

    /// <summary>View your own profile.</summary>
    /// <remarks>Available to both roles. Returns the profile of whoever is logged in.</remarks>
    /// <response code="200">The logged-in user's profile.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet("me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.FindAsync(HttpContext.RequireUserId());
        return user is null ? NotFound() : Ok(UserResponse.From(user));
    }

    /// <summary>Update your own profile.</summary>
    /// <remarks>
    /// Updates name and phone number. Email and role are deliberately not editable here:
    /// email is the login identifier, and changing role would bypass role-based access control.
    /// </remarks>
    /// <response code="200">Updated profile.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = await _db.Users.FindAsync(HttpContext.RequireUserId());
        if (user is null) return NotFound();

        user.FullName = request.FullName.Trim();
        user.Phone = request.Phone;
        await _db.SaveChangesAsync();

        return Ok(UserResponse.From(user));
    }

    /// <summary>Change your own password.</summary>
    /// <remarks>Requires the current password. The new password is re-hashed before storage.</remarks>
    /// <response code="200">Password changed.</response>
    /// <response code="400">Current password was incorrect, or the new password failed validation.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPut("me/password")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = await _db.Users.FindAsync(HttpContext.RequireUserId());
        if (user is null) return NotFound();

        if (!_hasher.Verify(request.CurrentPassword, user.PasswordHash))
            return BadRequest(new { message = "Current password is incorrect." });

        user.PasswordHash = _hasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync();

        return Ok(new { message = "Password updated successfully." });
    }
}
