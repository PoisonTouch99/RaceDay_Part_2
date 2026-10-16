using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Tests.Helpers;

/// <summary>Shared setup used across the test classes.</summary>
public static class TestHelpers
{
    /// <summary>A fresh in-memory database, isolated per test via a unique name.</summary>
    public static RaceDayDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<RaceDayDbContext>()
            .UseInMemoryDatabase($"RaceDayTests_{Guid.NewGuid()}")
            .EnableSensitiveDataLogging()
            .Options;

        return new RaceDayDbContext(options);
    }

    public static IPasswordHasher Hasher() => new PasswordHasher();

    /// <summary>
    /// Attaches a controller context. Pass a user to simulate a logged-in session,
    /// or null to simulate an anonymous request.
    /// </summary>
    public static void SetSession(this ControllerBase controller, User? user)
    {
        var httpContext = new DefaultHttpContext { Session = new FakeSession() };

        if (user is not null)
            httpContext.SignIn(user);

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
    }

    /// <summary>
    /// Runs the [SessionAuthorize] filter exactly as the framework would, and returns the
    /// result it produced (401 / 403), or null when access is allowed. This is how the
    /// role-enforcement tests verify authorization without spinning up a web server.
    /// </summary>
    public static IActionResult? RunAuthFilter(SessionAuthorizeAttribute attribute, User? user)
    {
        var httpContext = new DefaultHttpContext { Session = new FakeSession() };
        if (user is not null) httpContext.SignIn(user);

        var actionContext = new ActionContext(
            httpContext, new RouteData(), new ActionDescriptor());

        var filterContext = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());
        attribute.OnAuthorization(filterContext);

        return filterContext.Result;
    }

    // ---------- Entity builders ----------

    public static User MakeOrganiser(RaceDayDbContext db, string email = "org@raceday.co.za")
    {
        var user = new User
        {
            FullName = "Test Organiser",
            Email = email,
            PasswordHash = Hasher().Hash("Passw0rd!"),
            Role = Roles.Organiser
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static User MakeParticipant(RaceDayDbContext db, string email = "part@example.com")
    {
        var user = new User
        {
            FullName = "Test Participant",
            Email = email,
            PasswordHash = Hasher().Hash("Passw0rd!"),
            Role = Roles.Participant
        };
        db.Users.Add(user);
        db.SaveChanges();
        return user;
    }

    public static Event MakeEvent(RaceDayDbContext db, int organiserId, string name = "Test Run")
    {
        var ev = new Event
        {
            OrganiserId = organiserId,
            Name = name,
            Description = "Seeded test event",
            EventDate = DateTime.Today.AddDays(30),
            Location = "Pretoria, Gauteng",
            DistanceKm = 10m,
            EventType = EventTypes.Run,
            Status = EventStatuses.Published
        };
        db.Events.Add(ev);
        db.SaveChanges();
        return ev;
    }

    public static Category MakeCategory(RaceDayDbContext db, int eventId, int maxParticipants = 100)
    {
        var category = new Category
        {
            EventId = eventId,
            Name = "10km",
            CategoryType = CategoryTypes.Distance,
            DistanceKm = 10m,
            MaxParticipants = maxParticipants,
            EntryFee = 150m
        };
        db.Categories.Add(category);
        db.SaveChanges();
        return category;
    }
}
