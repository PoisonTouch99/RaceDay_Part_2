using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Authorization;
using RaceDay.Api.Data;
using RaceDay.Api.DTOs;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Race results. Organisers capture finish times and positions after an event;
/// Participants view their own results.
/// </summary>
[ApiController]
[Produces("application/json")]
public class ResultsController : ControllerBase
{
    private readonly RaceDayDbContext _db;
    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Capture a result for an enrolment (owning Organiser only).</summary>
    /// <remarks>
    /// Records the finish time and finishing position for one participant's entry.
    /// Set Status to DNF or DSQ if the participant did not finish or was disqualified,
    /// in which case FinishTime may be left null.
    /// </remarks>
    /// <response code="201">Result recorded.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">You are not an Organiser, or not the owner of the parent event.</response>
    /// <response code="404">No enrolment with that id.</response>
    /// <response code="409">A result has already been recorded for this enrolment.</response>
    [HttpPost("api/enrolments/{enrolmentId:int}/result")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(int enrolmentId, [FromBody] ResultRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var enrolment = await _db.Enrolments
            .Include(e => e.Participant)
            .Include(e => e.Category).ThenInclude(c => c!.Event)
            .Include(e => e.Result)
            .FirstOrDefaultAsync(e => e.EnrolmentId == enrolmentId);

        if (enrolment is null)
            return NotFound(new { message = $"Enrolment {enrolmentId} was not found." });

        if (enrolment.Category!.Event!.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only capture results for events that you organise." });

        if (enrolment.Result is not null)
            return Conflict(new { message = "A result has already been recorded for this enrolment." });

        var result = new Result
        {
            EnrolmentId = enrolmentId,
            FinishTime = request.FinishTime,
            Position = request.Position,
            Status = request.Status,
            RecordedAt = DateTime.UtcNow
        };

        _db.Results.Add(result);
        await _db.SaveChangesAsync();

        result.Enrolment = enrolment;
        return CreatedAtAction(nameof(GetById), new { id = result.ResultId }, ResultResponse.From(result));
    }

    /// <summary>Get a single result.</summary>
    /// <response code="200">The result.</response>
    /// <response code="404">No result with that id.</response>
    [HttpGet("api/results/{id:int}")]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);

        return result is null
            ? NotFound(new { message = $"Result {id} was not found." })
            : Ok(ResultResponse.From(result));
    }

    /// <summary>Correct an existing result (owning Organiser only).</summary>
    /// <remarks>Used for timing corrections or to change a status to DNF/DSQ after the fact.</remarks>
    /// <response code="200">Updated result.</response>
    /// <response code="403">Not the owner of the parent event.</response>
    /// <response code="404">No result with that id.</response>
    [HttpPut("api/results/{id:int}")]
    [SessionAuthorize(Roles.Organiser)]
    [ProducesResponseType(typeof(ResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, [FromBody] ResultRequest request)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var result = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event)
            .FirstOrDefaultAsync(r => r.ResultId == id);

        if (result is null) return NotFound(new { message = $"Result {id} was not found." });

        if (result.Enrolment!.Category!.Event!.OrganiserId != HttpContext.RequireUserId())
            return StatusCode(StatusCodes.Status403Forbidden,
                new { message = "You can only modify results for events that you organise." });

        result.FinishTime = request.FinishTime;
        result.Position = request.Position;
        result.Status = request.Status;

        await _db.SaveChangesAsync();
        return Ok(ResultResponse.From(result));
    }

    /// <summary>Leaderboard: all results for a category, ranked by finish time.</summary>
    /// <remarks>Open to both roles so anyone can view published race results.</remarks>
    /// <response code="200">Ranked array of results. DNF and DSQ entries are listed last.</response>
    /// <response code="404">No category with that id.</response>
    [HttpGet("api/categories/{categoryId:int}/results")]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForCategory(int categoryId)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == categoryId))
            return NotFound(new { message = $"Category {categoryId} was not found." });

        var results = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event)
            .Where(r => r.Enrolment!.CategoryId == categoryId)
            .ToListAsync();

        var ranked = results
            .OrderBy(r => r.Status == ResultStatuses.Finished ? 0 : 1)
            .ThenBy(r => r.FinishTime ?? TimeSpan.MaxValue)
            .Select(ResultResponse.From);

        return Ok(ranked);
    }

    /// <summary>View your own results (Participant only).</summary>
    /// <response code="200">Array of your results across all events.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">You are not a Participant.</response>
    [HttpGet("api/results/mine")]
    [SessionAuthorize(Roles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = HttpContext.RequireUserId();

        var results = await _db.Results
            .Include(r => r.Enrolment).ThenInclude(e => e!.Participant)
            .Include(r => r.Enrolment).ThenInclude(e => e!.Category).ThenInclude(c => c!.Event)
            .Where(r => r.Enrolment!.ParticipantId == participantId)
            .OrderByDescending(r => r.RecordedAt)
            .ToListAsync();

        return Ok(results.Select(ResultResponse.From));
    }
}
