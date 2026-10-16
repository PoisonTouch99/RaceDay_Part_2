using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using RaceDay.Api.Controllers;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;
using RaceDay.Tests.Helpers;
using Xunit;

namespace RaceDay.Tests;

/// <summary>
/// Results: Organisers capture finish times and positions for their own events;
/// Participants can view their own results.
/// </summary>
public class ResultsTests
{
    private static Enrolment SeedEnrolment(
        Api.Data.RaceDayDbContext db, User organiser, User participant, out Category category)
    {
        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        category = TestHelpers.MakeCategory(db, ev.EventId);

        var enrolment = new Enrolment
        {
            ParticipantId = participant.UserId,
            CategoryId = category.CategoryId,
            BibNumber = "BIB0001",
            Status = EnrolmentStatuses.Confirmed
        };
        db.Enrolments.Add(enrolment);
        db.SaveChanges();
        return enrolment;
    }

    [Fact]
    public async Task Organiser_CanCaptureResultForTheirOwnEvent()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var enrolment = SeedEnrolment(db, organiser, participant, out _);

        var controller = new ResultsController(db);
        controller.SetSession(organiser);

        var result = await controller.Create(enrolment.EnrolmentId, new ResultRequest
        {
            FinishTime = new TimeSpan(0, 52, 14),
            Position = 7,
            Status = ResultStatuses.Finished
        });

        var created = Assert.IsType<CreatedAtActionResult>(result);
        var body = Assert.IsType<ResultResponse>(created.Value);

        Assert.Equal(new TimeSpan(0, 52, 14), body.FinishTime);
        Assert.Equal(7, body.Position);
        Assert.Single(db.Results);
    }

    [Fact]
    public async Task Organiser_CannotCaptureResultForAnotherOrganisersEvent_Returns403()
    {
        var db = TestHelpers.NewDb();
        var owner = TestHelpers.MakeOrganiser(db, "owner@raceday.co.za");
        var intruder = TestHelpers.MakeOrganiser(db, "intruder@raceday.co.za");
        var participant = TestHelpers.MakeParticipant(db);
        var enrolment = SeedEnrolment(db, owner, participant, out _);

        var controller = new ResultsController(db);
        controller.SetSession(intruder);

        var result = await controller.Create(enrolment.EnrolmentId, new ResultRequest
        {
            FinishTime = new TimeSpan(0, 40, 0),
            Position = 1,
            Status = ResultStatuses.Finished
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Empty(db.Results);
    }

    [Fact]
    public async Task CapturingSecondResultForSameEnrolment_ReturnsConflict()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var enrolment = SeedEnrolment(db, organiser, participant, out _);

        var controller = new ResultsController(db);
        controller.SetSession(organiser);

        var request = new ResultRequest
        {
            FinishTime = new TimeSpan(0, 52, 14),
            Position = 7,
            Status = ResultStatuses.Finished
        };

        await controller.Create(enrolment.EnrolmentId, request);
        var second = await controller.Create(enrolment.EnrolmentId, request);

        Assert.IsType<ConflictObjectResult>(second);
        Assert.Single(db.Results);
    }

    [Fact]
    public async Task Participant_CanViewOwnResults()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var participant = TestHelpers.MakeParticipant(db);
        var enrolment = SeedEnrolment(db, organiser, participant, out _);

        var resultsController = new ResultsController(db);
        resultsController.SetSession(organiser);
        await resultsController.Create(enrolment.EnrolmentId, new ResultRequest
        {
            FinishTime = new TimeSpan(0, 52, 14),
            Position = 7,
            Status = ResultStatuses.Finished
        });

        resultsController.SetSession(participant);
        var result = await resultsController.GetMine();

        var ok = Assert.IsType<OkObjectResult>(result);
        var results = Assert.IsAssignableFrom<IEnumerable<ResultResponse>>(ok.Value).ToList();

        Assert.Single(results);
        Assert.Equal("BIB0001", results[0].BibNumber);
    }

    [Fact]
    public async Task Leaderboard_RanksFinishersByTime_WithDnfLast()
    {
        var db = TestHelpers.NewDb();
        var organiser = TestHelpers.MakeOrganiser(db);
        var fast = TestHelpers.MakeParticipant(db, "fast@example.com");
        var slow = TestHelpers.MakeParticipant(db, "slow@example.com");
        var dnf = TestHelpers.MakeParticipant(db, "dnf@example.com");

        var ev = TestHelpers.MakeEvent(db, organiser.UserId);
        var category = TestHelpers.MakeCategory(db, ev.EventId);

        var enrolments = new[]
        {
            new Enrolment { ParticipantId = fast.UserId, CategoryId = category.CategoryId, BibNumber = "BIB0001", Status = EnrolmentStatuses.Confirmed },
            new Enrolment { ParticipantId = slow.UserId, CategoryId = category.CategoryId, BibNumber = "BIB0002", Status = EnrolmentStatuses.Confirmed },
            new Enrolment { ParticipantId = dnf.UserId,  CategoryId = category.CategoryId, BibNumber = "BIB0003", Status = EnrolmentStatuses.Confirmed }
        };
        db.Enrolments.AddRange(enrolments);
        db.SaveChanges();

        db.Results.AddRange(
            new Result { EnrolmentId = enrolments[1].EnrolmentId, FinishTime = new TimeSpan(1, 10, 0), Status = ResultStatuses.Finished },
            new Result { EnrolmentId = enrolments[0].EnrolmentId, FinishTime = new TimeSpan(0, 45, 0), Status = ResultStatuses.Finished },
            new Result { EnrolmentId = enrolments[2].EnrolmentId, FinishTime = null, Status = ResultStatuses.DNF });
        db.SaveChanges();

        var controller = new ResultsController(db);
        controller.SetSession(null);   // leaderboard is public

        var result = await controller.GetForCategory(category.CategoryId);

        var ok = Assert.IsType<OkObjectResult>(result);
        var ranked = Assert.IsAssignableFrom<IEnumerable<ResultResponse>>(ok.Value).ToList();

        Assert.Equal("BIB0001", ranked[0].BibNumber);   // fastest first
        Assert.Equal("BIB0002", ranked[1].BibNumber);
        Assert.Equal("BIB0003", ranked[2].BibNumber);   // DNF last
    }
}
