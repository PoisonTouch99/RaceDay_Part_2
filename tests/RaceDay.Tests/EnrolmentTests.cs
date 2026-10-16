using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Event enrolments: a Participant can enter an event by selecting a category,
/// and the link between participant, event and category is recorded correctly.
/// </summary>
public class EnrolmentTests
{
    [Fact]
    public async Task Participant_CanEnrol_AndEnrolmentIsRecordedCorrectly()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId, "Pretoria 10km");
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var controller = new EnrolmentsController(db);
        controller.SetSession(participant);

        var result = await controller.Enrol(category.CategoryId);

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<EnrolmentResponse>(created.Value);

        // The link between participant, event and category is all present.
        Assert.Equal(participant.UserId, body.ParticipantId);
        Assert.Equal(category.CategoryId, body.CategoryId);
        Assert.Equal(ev.EventId, body.EventId);
        Assert.Equal("Pretoria 10km", body.EventName);
        Assert.False(string.IsNullOrWhiteSpace(body.BibNumber));
        Assert.Equal(EnrolmentStatuses.Confirmed, body.Status);

        // And it is persisted.
        var stored = Assert.Single(db.Enrolments);
        Assert.Equal(participant.UserId, stored.ParticipantId);
        Assert.Equal(category.CategoryId, stored.CategoryId);
    }

    [Fact]
    public async Task Enrolling_CreatesPendingPaymentForEntryFee()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var controller = new EnrolmentsController(db);
        controller.SetSession(participant);

        await controller.Enrol(category.CategoryId);

        var payment = Assert.Single(db.Payments);
        Assert.Equal(150m, payment.Amount);
        Assert.Equal(PaymentStatuses.Pending, payment.PaymentStatus);
    }

    [Fact]
    public async Task Participant_CannotEnrolTwiceInSameCategory_ReturnsConflict()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var controller = new EnrolmentsController(db);
        controller.SetSession(participant);

        await controller.Enrol(category.CategoryId);
        var second = await controller.Enrol(category.CategoryId);

        Assert.IsType<ConflictObjectResult>(second);
        Assert.Single(db.Enrolments);   // still only one
    }

    [Fact]
    public async Task EnrollingInFullCategory_ReturnsConflict()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var first = TestHelpers.MakeParticipant(db, "first@example.com");
        var second = TestHelpers.MakeParticipant(db, "second@example.com");
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId, maxParticipants: 1);

        var controller = new EnrolmentsController(db);

        controller.SetSession(first);
        await controller.Enrol(category.CategoryId);

        controller.SetSession(second);
        var result = await controller.Enrol(category.CategoryId);

        Assert.IsType<ConflictObjectResult>(result);
        Assert.Single(db.Enrolments);
    }

    [Fact]
    public async Task EnrollingInNonExistentCategory_Returns404()
    {
        var db = TestHelpers.NewDb();
        var participant = TestHelpers.MakeParticipant(db);
        var controller = new EnrolmentsController(db);
        controller.SetSession(participant);

        var result = await controller.Enrol(9999);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Participant_CanViewOnlyTheirOwnEnrolments()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var alice = TestHelpers.MakeParticipant(db, "alice@example.com");
        var bob = TestHelpers.MakeParticipant(db, "bob@example.com");
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var controller = new EnrolmentsController(db);

        controller.SetSession(alice);
        await controller.Enrol(category.CategoryId);

        controller.SetSession(bob);
        var result = await controller.GetMine();

        var ok = Assert.IsType<OkObjectResult>(result);
        var enrolments = Assert.IsAssignableFrom<IEnumerable<EnrolmentResponse>>(ok.Value);
        Assert.Empty(enrolments);   // Bob sees none of Alice's
    }

    [Fact]
    public async Task Organiser_CanViewEnrolmentsForTheirOwnEvent()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var controller = new EnrolmentsController(db);
        controller.SetSession(participant);
        await controller.Enrol(category.CategoryId);

        controller.SetSession(organiser);
        var result = await controller.GetForEvent(ev.EventId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var enrolments = Assert.IsAssignableFrom<IEnumerable<EnrolmentResponse>>(ok.Value).ToList();

        Assert.Single(enrolments);
        Assert.Equal(participant.UserId, enrolments[0].ParticipantId);
    }

    [Fact]
    public async Task Organiser_CannotViewEnrolmentsForAnotherOrganisersEvent_Returns403()
    {
        var db = TestHelpers.NewDb();
        var owner = TestHelpers.MakeOrganiser(db, "owner@raceday.co.za");
        var intruder = TestHelpers.MakeOrganiser(db, "intruder@raceday.co.za");
        var ev = TestHelpers.MakeEvent(db, owner.UserId);

        var controller = new EnrolmentsController(db);
        controller.SetSession(intruder);

        var result = await controller.GetForEvent(ev.EventId);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task Participant_CanCancelTheirOwnEnrolment()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var controller = new EnrolmentsController(db);
        controller.SetSession(participant);

        var created = (CreatedAtActionResult)await controller.Enrol(category.CategoryId);
        var enrolmentId = ((EnrolmentResponse)created.Value!).EnrolmentId;

        var result = await controller.Cancel(enrolmentId);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal(EnrolmentStatuses.Cancelled, db.Enrolments.Single().Status);
    }
}
