using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Authorization;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>Registration and login: success and failure paths, and password hashing.</summary>
public class AuthTests
{
    private static AuthController NewController(out Api.Data.RaceDayDbContext db)
    {
        db = TestHelpers.NewDb();
        return new AuthController(db, TestHelpers.Hasher());
    }

    // ---------- Registration ----------

    [Theory]
    [InlineData(Roles.Organiser)]
    [InlineData(Roles.Participant)]
    public async Task Register_WithValidDetails_CreatesAccountForEitherRole(string role)
    {
        var controller = NewController(out var db);
        controller.SetSession(null);

        var result = await controller.Register(new RegisterRequest
        {
            FullName = "New User",
            Email = "new.user@example.com",
            Password = "Passw0rd!",
            Role = role
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<UserResponse>(created.Value);

        Assert.Equal(role, body.Role);
        Assert.Equal("new.user@example.com", body.Email);
        Assert.Single(db.Users);
    }

    [Fact]
    public async Task Register_StoresHashedPassword_NeverPlainText()
    {
        var controller = NewController(out var db);
        controller.SetSession(null);

        await controller.Register(new RegisterRequest
        {
            FullName = "Hash Check",
            Email = "hash.check@example.com",
            Password = "Passw0rd!",
            Role = Roles.Participant
        });

        var stored = db.Users.Single();

        Assert.NotEqual("Passw0rd!", stored.PasswordHash);
        Assert.DoesNotContain("Passw0rd!", stored.PasswordHash);
        // PBKDF2 format is iterations.salt.subkey
        Assert.Equal(3, stored.PasswordHash.Split('.').Length);
        Assert.True(TestHelpers.Hasher().Verify("Passw0rd!", stored.PasswordHash));
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var controller = NewController(out var db);
        controller.SetSession(null);
        TestHelpers.MakeParticipant(db, "taken@example.com");

        var result = await controller.Register(new RegisterRequest
        {
            FullName = "Second User",
            Email = "taken@example.com",
            Password = "Passw0rd!",
            Role = Roles.Participant
        });

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Users);   // no second account created
    }

    // ---------- Login ----------

    [Fact]
    public async Task Login_WithCorrectCredentials_StoresUserIdAndRoleInSession()
    {
        var controller = NewController(out var db);
        controller.SetSession(null);
        var user = TestHelpers.MakeOrganiser(db, "login.ok@raceday.co.za");

        var result = await controller.Login(new LoginRequest
        {
            Email = "login.ok@raceday.co.za",
            Password = "Passw0rd!"
        });

        Assert.IsType<OkObjectResult>(result);

        // The session must carry the user id and role for subsequent requests.
        var ctx = controller.ControllerContext.HttpContext;
        Assert.Equal(user.UserId, ctx.Session.GetInt32(SessionKeys.UserId));
        Assert.Equal(Roles.Organiser, ctx.Session.GetString(SessionKeys.Role));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorisedAndNoSession()
    {
        var controller = NewController(out var db);
        controller.SetSession(null);
        TestHelpers.MakeParticipant(db, "wrong.pass@example.com");

        var result = await controller.Login(new LoginRequest
        {
            Email = "wrong.pass@example.com",
            Password = "NotThePassword!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Null(controller.ControllerContext.HttpContext.Session.GetInt32(SessionKeys.UserId));
    }

    [Fact]
    public async Task Login_WithUnknownEmail_ReturnsUnauthorised()
    {
        var controller = NewController(out _);
        controller.SetSession(null);

        var result = await controller.Login(new LoginRequest
        {
            Email = "nobody@example.com",
            Password = "Passw0rd!"
        });

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    // ---------- Logout ----------

    [Fact]
    public void Logout_ClearsTheSession()
    {
        var controller = NewController(out var db);
        var user = TestHelpers.MakeParticipant(db);
        controller.SetSession(user);

        var result = controller.Logout();

        Assert.IsType<NoContentResult>(result);
        Assert.Null(controller.ControllerContext.HttpContext.Session.GetInt32(SessionKeys.UserId));
    }
}
