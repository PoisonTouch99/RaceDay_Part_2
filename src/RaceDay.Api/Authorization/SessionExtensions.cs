using RaceDay.Api.Models;

namespace RaceDay.Api.Authorization;

/// <summary>Convenience accessors for the current session user inside controllers.</summary>
public static class SessionExtensions
{
    public static int? GetUserId(this HttpContext ctx) => ctx.Session.GetInt32(SessionKeys.UserId);

    /// <summary>Use only inside [SessionAuthorize] actions, where presence is guaranteed.</summary>
    public static int RequireUserId(this HttpContext ctx) =>
        ctx.Session.GetInt32(SessionKeys.UserId)
        ?? throw new InvalidOperationException("No user id in session.");

    public static string? GetRole(this HttpContext ctx) => ctx.Session.GetString(SessionKeys.Role);

    public static bool IsOrganiser(this HttpContext ctx) => ctx.GetRole() == Roles.Organiser;
    public static bool IsParticipant(this HttpContext ctx) => ctx.GetRole() == Roles.Participant;

    public static void SignIn(this HttpContext ctx, User user)
    {
        ctx.Session.SetInt32(SessionKeys.UserId, user.UserId);
        ctx.Session.SetString(SessionKeys.Role, user.Role);
        ctx.Session.SetString(SessionKeys.FullName, user.FullName);
    }

    public static void SignOut(this HttpContext ctx) => ctx.Session.Clear();
}
