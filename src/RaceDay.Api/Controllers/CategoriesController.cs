using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Age and distance categories within an event (e.g. "Under 20", "Senior", "10km", "21km").
/// Both roles can view; only the owning Organiser can manage.
/// </summary>
[ApiController]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>List the categories available for an event.</summary>
    /// <remarks>Open to both roles so Participants can choose a category before enrolling.</remarks>
    /// <response code="200">Array of categories with current enrolment counts.</response>
    /// <response code="404">No event with that id.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [ProducesResponseType(typeof(IEnumerable<CategoryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFound(new { message = $"Event {eventId} was not found." });

        var categories = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .Where(c => c.EventId == eventId)
            .ToListAsync();

        return Ok(categories.Select(CategoryResponse.From));
    }

    /// <summary>Get a single category.</summary>
    /// <response code="200">The category.</response>
    /// <response code="404">No category with that id.</response>
    [HttpGet("api/categories/{id:int}")]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        return category is null
            ? NotFound(new { message = $"Category {id} was not found." })
            : Ok(CategoryResponse.From(category));
    }

    /// <summary>Add a category to an event (owning Organiser only).</summary>
    /// <remarks>
    /// Supply CategoryType "Distance" with DistanceKm, or "Age" with MinAge/MaxAge.
    /// The relevant field is validated for the type supplied.
    /// </remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Validation failed, e.g. an Age category without an age range.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not an Organiser, or not the owner of this event.</response>
    /// <response code="404">No event with that id.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, [FromBody] CategoryRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var ev = await _db.Events.FindAsync(eventId);
        if (ev is null) return NotFound(new { message = $"Event {eventId} was not found." });

        if (ev.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only add categories to events that you organise." });

        if (request.CategoryType == CategoryTypes.Distance && request.DistanceKm is null)
            return BadRequest(new { message = "A Distance category requires DistanceKm." });

        if (request.CategoryType == CategoryTypes.Age && (request.MinAge is null || request.MaxAge is null))
            return BadRequest(new { message = "An Age category requires both MinAge and MaxAge." });

        var category = new Category
        {
            EventId = eventId,
            Name = request.Name.Trim(),
            CategoryType = request.CategoryType,
            DistanceKm = request.CategoryType == CategoryTypes.Distance ? request.DistanceKm : null,
            MinAge = request.CategoryType == CategoryTypes.Age ? request.MinAge : null,
            MaxAge = request.CategoryType == CategoryTypes.Age ? request.MaxAge : null,
            MaxParticipants = request.MaxParticipants,
            EntryFee = request.EntryFee
        };

        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = category.CategoryId },
            CategoryResponse.From(category));
    }

    /// <summary>Update a category (owning Organiser only).</summary>
    /// <response code="200">Updated category.</response>
    /// <response code="403">You are not an Organiser, or not the owner of the parent event.</response>
    /// <response code="404">No category with that id.</response>
    [HttpPut("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(CategoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var category = await _db.Categories
            .Include(c => c.Event)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return NotFound(new { message = $"Category {id} was not found." });

        if (category.Event!.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only modify categories on events that you organise." });

        category.Name = request.Name.Trim();
        category.CategoryType = request.CategoryType;
        category.DistanceKm = request.CategoryType == CategoryTypes.Distance ? request.DistanceKm : null;
        category.MinAge = request.CategoryType == CategoryTypes.Age ? request.MinAge : null;
        category.MaxAge = request.CategoryType == CategoryTypes.Age ? request.MaxAge : null;
        category.MaxParticipants = request.MaxParticipants;
        category.EntryFee = request.EntryFee;

        await _db.SaveChangesAsync();
        return Ok(CategoryResponse.From(category));
    }

    /// <summary>Delete a category (owning Organiser only).</summary>
    /// <remarks>Blocked if participants have already enrolled in it.</remarks>
    /// <response code="204">Category deleted.</response>
    /// <response code="403">You are not an Organiser, or not the owner of the parent event.</response>
    /// <response code="404">No category with that id.</response>
    /// <response code="409">The category already has enrolments.</response>
    [HttpDelete("api/categories/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Event)
            .Include(c => c.Enrolments)
            .FirstOrDefaultAsync(c => c.CategoryId == id);

        if (category is null) return NotFound(new { message = $"Category {id} was not found." });

        if (category.Event!.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only delete categories on events that you organise." });

        if (category.Enrolments.Any())
            return Conflict(new { message = "This category already has enrolments and cannot be deleted." });

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
