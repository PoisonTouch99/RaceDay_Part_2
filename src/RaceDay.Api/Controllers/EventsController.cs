using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Event management. Both roles can browse published events;
/// only Organisers can create, update or delete, and only their own events.
/// </summary>
[ApiController]
[Route("api/events")]
[Produces("application/json")]
public class EventsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>List all published events.</summary>
    /// <remarks>
    /// Public browsing endpoint, available to both roles and to anonymous callers.
    /// Optional filters narrow by location and event type.
    /// </remarks>
    /// <param name="location">Optional partial match on location.</param>
    /// <param name="eventType">Optional filter: Run, Walk or Cycle.</param>
    /// <response code="200">Array of published events.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] string? location, [FromQuery] string? eventType)
    {
        var query = _db.Events
            .Include(e => e.Organiser)
            .Where(e => e.Status == EventStatuses.Published);

        if (!string.IsNullOrWhiteSpace(location))
            query = query.Where(e => e.Location.Contains(location));

        if (!string.IsNullOrWhiteSpace(eventType))
            query = query.Where(e => e.EventType == eventType);

        var events = await query.OrderBy(e => e.EventDate).ToListAsync();
        return Ok(events.Select(e => EventResponse.From(e)));
    }

    /// <summary>Get one event, including its categories.</summary>
    /// <response code="200">The event with its category list.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Organiser)
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments)
            .FirstOrDefaultAsync(e => e.EventId == id);

        return ev is null
            ? NotFound(new { message = $"Event {id} was not found." })
            : Ok(EventResponse.From(ev, includeCategories: true));
    }

    /// <summary>List events you organise (Organiser only).</summary>
    /// <remarks>Includes drafts and closed events, which are hidden from the public list.</remarks>
    /// <response code="200">Array of your events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not an Organiser.</response>
    [HttpGet("mine")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var organiserId = HttpContext.RequireUserId();
        var events = await _db.Events
            .Include(e => e.Categories)
            .Where(e => e.OrganiserId == organiserId)
            .OrderBy(e => e.EventDate)
            .ToListAsync();

        return Ok(events.Select(e => EventResponse.From(e, includeCategories: true)));
    }

    /// <summary>Create an event (Organiser only).</summary>
    /// <remarks>
    /// The new event is owned by the logged-in Organiser. Captures name, description, date,
    /// location, distance and event type. Defaults to Draft status unless Published is supplied.
    /// </remarks>
    /// <response code="201">Event created.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not an Organiser.</response>
    [HttpPost]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] EventRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ev = new Event
        {
            OrganiserId = HttpContext.RequireUserId(),
            Name = request.Name.Trim(),
            Description = request.Description,
            EventDate = request.EventDate,
            Location = request.Location.Trim(),
            DistanceKm = request.DistanceKm,
            EventType = request.EventType,
            Status = request.Status ?? EventStatuses.Draft,
            CreatedAt = DateTime.UtcNow
        };

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, EventResponse.From(ev));
    }

    /// <summary>Update an event (owning Organiser only).</summary>
    /// <response code="200">Updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not an Organiser, or not the owner of this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPut("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(EventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] EventRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ev = await _db.Events.FindAsync(id);
        if (ev is null) return NotFound(new { message = $"Event {id} was not found." });

        // Ownership check: an Organiser may only edit their own events.
        if (ev.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only modify events that you organise." });

        ev.Name = request.Name.Trim();
        ev.Description = request.Description;
        ev.EventDate = request.EventDate;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm;
        ev.EventType = request.EventType;
        if (request.Status is not null) ev.Status = request.Status;

        await _db.SaveChangesAsync();
        return Ok(EventResponse.From(ev));
    }

    /// <summary>Delete an event (owning Organiser only).</summary>
    /// <remarks>Blocked if participants have already enrolled, to avoid orphaning their entries.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not an Organiser, or not the owner of this event.</response>
    /// <response code="404">No event with that id.</response>
    /// <response code="409">The event already has enrolments.</response>
    [HttpDelete("{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments)
            .FirstOrDefaultAsync(e => e.EventId == id);

        if (ev is null) return NotFound(new { message = $"Event {id} was not found." });

        if (ev.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete events that you organise." });

        if (ev.Categories.Any(c => c.Enrolments.Any()))
            return Conflict(new { message = "This event already has enrolments and cannot be deleted." });

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
