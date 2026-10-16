using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Event enrolments. Participants enter an event by selecting a category;
/// Organisers can view all enrolments for events they own.
/// </summary>
[ApiController]
[Produces("application/json")]
public class EnrolmentsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enter an event by selecting a category (Participant only).</summary>
    /// <remarks>
    /// Records the link between the Participant, the event and the selected category,
    /// and generates a unique bib number. A pending payment record is created for the entry fee.
    /// Enrolling twice in the same category is rejected, as is enrolling in a full category.
    /// </remarks>
    /// <response code="201">Enrolment created, including the assigned bib number.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not a Participant. Organisers cannot enrol.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">Already enrolled in this category, or the category is full.</response>
    [HttpPost("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var participantId = HttpContext.RequireUserId();

        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category is null)
            return NotFound(new { message = $"Category {categoryId} was not found." });

        if (category.Enrolments.Any(e => e.ParticipantId == participantId
                                      && e.Status != EnrolmentStatuses.Cancelled))
            return Conflict(new { message = "You are already enrolled in this category." });

        var activeCount = category.Enrolments.Count(e => e.Status != EnrolmentStatuses.Cancelled);
        if (activeCount >= category.MaxParticipants)
            return Conflict(new { message = "This category is full." });

        var enrolment = new Enrolment
        {
            ParticipantId = participantId,
            CategoryId = categoryId,
            BibNumber = await GenerateBibNumberAsync(),
            EnrolmentDate = DateTime.UtcNow,
            Status = EnrolmentStatuses.Confirmed
        };

        _db.Enrolments.Add(enrolment);
        await _db.SaveChangesAsync();

        // Entry fee is recorded as a pending payment alongside the enrolment.
        _db.Payments.Add(new Payment
        {
            EnrolmentId = enrolment.EnrolmentId,
            Amount = category.EntryFee,
            PaymentStatus = PaymentStatuses.Pending
        });
        await _db.SaveChangesAsync();

        var created = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .FirstAsync(e => e.EnrolmentId == enrolment.EnrolmentId);

        return CreatedAtAction(nameof(GetById), new { id = enrolment.EnrolmentId },
            EnrolmentResponse.From(created));
    }

    /// <summary>Get a single enrolment.</summary>
    /// <remarks>
    /// Visible to the Participant who owns it, or to the Organiser of the parent event.
    /// </remarks>
    /// <response code="200">The enrolment.</response>
    /// <response code="403">Not your enrolment and not your event.</response>
    /// <response code="404">No enrolment with that id.</response>
    [HttpGet("api/enrolments/{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(typeof(EnrolmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFound(new { message = $"Enrolment {id} was not found." });

        var userId = HttpContext.RequireUserId();
        var isOwner = enrolment.ParticipantId == userId;
        var isEventOrganiser = enrolment.Category?.Event?.OrganiserId == userId;

        if (!isOwner && !isEventOrganiser)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You do not have access to this enrolment." });

        return Ok(EnrolmentResponse.From(enrolment));
    }

    /// <summary>List your own enrolments (Participant only).</summary>
    /// <response code="200">Array of your enrolments across all events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not a Participant.</response>
    [HttpGet("api/enrolments/mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = HttpContext.RequireUserId();

        var enrolments = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .Where(e => e.ParticipantId == participantId)
            .OrderByDescending(e => e.EnrolmentDate)
            .ToListAsync();

        return Ok(enrolments.Select(EnrolmentResponse.From));
    }

    /// <summary>List all enrolments in a category (owning Organiser only).</summary>
    /// <remarks>Used by Organisers to manage the entry list for their own events.</remarks>
    /// <response code="200">Array of enrolments.</response>
    /// <response code="403">You are not an Organiser, or not the owner of the parent event.</response>
    /// <response code="404">No category with that id.</response>
    [HttpGet("api/categories/{categoryId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForCategory(int categoryId)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == categoryId);

        if (category is null) return NotFound(new { message = $"Category {categoryId} was not found." });

        if (category.Event!.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view enrolments for events that you organise." });

        var enrolments = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .Where(e => e.CategoryId == categoryId)
            .ToListAsync();

        return Ok(enrolments.Select(EnrolmentResponse.From));
    }

    /// <summary>List all enrolments across one of your events (owning Organiser only).</summary>
    /// <response code="200">Array of enrolments for every category in the event.</response>
    /// <response code="403">Not the owner of this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = $"Event {eventId} was not found." });

        if (ev.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only view enrolments for events that you organise." });

        var enrolments = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .Where(e => e.Category!.EventId == eventId)
            .ToListAsync();

        return Ok(enrolments.Select(EnrolmentResponse.From));
    }

    /// <summary>Cancel an enrolment.</summary>
    /// <remarks>
    /// A Participant may cancel their own enrolment; the Organiser of the event may cancel any
    /// enrolment in it. The record is marked Cancelled rather than deleted, so the history is kept.
    /// </remarks>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="403">Not your enrolment and not your event.</response>
    /// <response code="404">No enrolment with that id.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [SessionAuthorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var enrolment = await _db.Enrolments
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(e => e.EnrolmentId == id);

        if (enrolment is null) return NotFound(new { message = $"Enrolment {id} was not found." });

        var userId = HttpContext.RequireUserId();
        var isOwner = enrolment.ParticipantId == userId;
        var isEventOrganiser = enrolment.Category?.Event?.OrganiserId == userId;

        if (!isOwner && !isEventOrganiser)
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You cannot cancel this enrolment." });

        enrolment.Status = EnrolmentStatuses.Cancelled;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Generates the next sequential bib number, e.g. BIB0007.</summary>
    private async Task<string> GenerateBibNumberAsync()
    {
        var count = await _db.Enrolments.CountAsync();
        return $"BIB{count + 1:D4}";
    }
}
