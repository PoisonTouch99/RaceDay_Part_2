using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Event management: Organisers can create, update and delete their own events,
/// and are blocked from touching events owned by someone else.
/// </summary>
public class EventManagementTests
{
    private static EventRequest ValidRequest(string name = "Pretoria Half") => new()
    {
        Name = name,
        Description = "A half marathon through Pretoria.",
        EventDate = DateTime.Today.AddDays(60),
        Location = "Pretoria, Gauteng",
        DistanceKm = 21.1m,
        EventType = EventTypes.Run,
        Status = EventStatuses.Published
    };

    [Fact]
    public async Task Organiser_CanCreateEvent_AndIsRecordedAsOwner()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var controller = new EventsController(db);
        controller.SetSession(organiser);

        var result = await controller.Create(ValidRequest());

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<EventResponse>(created.Value);

        Assert.Equal("Pretoria Half", body.Name);
        Assert.Equal(organiser.UserId, body.OrganiserId);
        Assert.Equal(EventTypes.Run, body.EventType);
        Assert.Equal(21.1m, body.DistanceKm);

        // Persisted correctly
        var stored = Assert.Single(db.Events);
        Assert.Equal(organiser.UserId, stored.OrganiserId);
    }

    [Fact]
    public async Task Organiser_CanUpdateTheirOwnEvent()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var controller = new EventsController(db);
        controller.SetSession(organiser);

        var request = ValidRequest("Updated Name");
        request.EventType = EventTypes.Cycle;

        var result = await controller.Update(ev.EventId, request);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<EventResponse>(ok.Value);

        Assert.Equal("Updated Name", body.Name);
        Assert.Equal(EventTypes.Cycle, body.EventType);
    }

    [Fact]
    public async Task Organiser_CannotUpdateAnotherOrganisersEvent_Returns403()
    {
        var db = TestHelpers.NewDb();
        var owner = TestHelpers.MakeOrganiser(db, "owner@raceday.co.za");
        var intruder = TestHelpers.MakeOrganiser(db, "intruder@raceday.co.za");
        var ev = TestHelpers.MakeEvent(db, owner.UserId);

        var controller = new EventsController(db);
        controller.SetSession(intruder);

        var result = await controller.Update(ev.EventId, ValidRequest("Hijacked"));

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);

        // The event is unchanged
        Assert.Equal("Test Run", db.Events.Single().Name);
    }

    [Fact]
    public async Task Organiser_CanDeleteTheirOwnEventWithNoEnrolments()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var controller = new EventsController(db);
        controller.SetSession(organiser);

        var result = await controller.Delete(ev.EventId);

        Assert.IsType<NoContentResult>(result);
        Assert.Empty(db.Events);
    }

    [Fact]
    public async Task DeletingEventWithEnrolments_ReturnsConflict()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        db.Enrolments.Add(new Enrolment
        {
            ParticipantId = participant.UserId,
            CategoryId = category.CategoryId,
            BibNumber = "BIB0001",
            Status = EnrolmentStatuses.Confirmed
        });
        db.SaveChanges();

        var controller = new EventsController(db);
        controller.SetSession(organiser);

        var result = await controller.Delete(ev.EventId);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Events);   // not deleted
    }

    [Fact]
    public async Task UpdatingNonExistentEvent_Returns404()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var controller = new EventsController(db);
        controller.SetSession(organiser);

        var result = await controller.Update(9999, ValidRequest());

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyPublishedEvents()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        TestHelpers.MakeEvent(db, organiser.UserId, "Published Event");

        db.Events.Add(new Event
        {
            OrganiserId = organiser.UserId,
            Name = "Draft Event",
            EventDate = DateTime.Today.AddDays(10),
            Location = "Durban",
            DistanceKm = 5m,
            EventType = EventTypes.Walk,
            Status = EventStatuses.Draft
        });
        db.SaveChanges();

        var controller = new EventsController(db);
        controller.SetSession(null);   // public browsing

        var result = await controller.GetAll(null, null);

        var ok = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsAssignableFrom<IEnumerable<EventResponse>>(ok.Value).ToList();

        Assert.Single(events);
        Assert.Equal("Published Event", events[0].Name);
    }

    [Fact]
    public async Task GetMine_ReturnsOnlyTheLoggedInOrganisersEvents()
    {
        var db = TestHelpers.NewDb();
        var mine = TestHelpers.MakeOrganiser(db, "mine@raceday.co.za");
        var theirs = TestHelpers.MakeOrganiser(db, "theirs@raceday.co.za");
        TestHelpers.MakeEvent(db, mine.UserId, "My Event");
        TestHelpers.MakeEvent(db, theirs.UserId, "Their Event");

        var controller = new EventsController(db);
        controller.SetSession(mine);

        var result = await controller.GetMine();

        var ok = Assert.IsType<OkObjectResult>(result);
        var events = Assert.IsAssignableFrom<IEnumerable<EventResponse>>(ok.Value).ToList();

        Assert.Single(events);
        Assert.Equal("My Event", events[0].Name);
    }
}
