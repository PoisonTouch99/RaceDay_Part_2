using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace RaceDay.Api.Authorization;

/// <summary>
/// Enforces server-side session authentication and (optionally) role membership.
/// Reads the user id and role written to the session at login.
///   [SessionAuthorize]                      -> any logged-in user
///   [SessionAuthorize(Roles.Organiser)]     -> Organisers only
/// Returns 401 when not logged in, 403 when logged in with the wrong role.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class SessionAuthorizeAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _allowedRoles;

    public SessionAuthorizeAttribute(params string[] allowedRoles) => _allowedRoles = allowedRoles;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var session = context.HttpContext.Session;
        var userId = session.GetInt32(SessionKeys.UserId);
        var role = session.GetString(SessionKeys.Role);

        if (userId is null || string.IsNullOrEmpty(role))
        {
            context.Result = new UnauthorizedObjectResult(
                new { message = "You must be logged in to access this resource." });
            return;
        }

        if (_allowedRoles.Length > 0 && !_allowedRoles.Contains(role))
        {
            context.Result = new ObjectResult(
                new { message = $"This resource requires one of the following roles: {string.Join(", ", _allowedRoles)}." })
            { StatusCode = StatusCodes.Status403Forbidden };
        }
    }
}

/// <summary>Session key names, kept in one place to avoid typos.</summary>
public static class SessionKeys
{
    public const string UserId = "UserId";
    public const string Role = "Role";
    public const string FullName = "FullName";
}
