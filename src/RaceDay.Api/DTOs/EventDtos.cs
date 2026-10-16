using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class EventRequest
{
    /// <example>Johannesburg City Run</example>
    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    /// <example>Annual road race through the Johannesburg CBD.</example>
    [MaxLength(1000)]
    public string? Description { get; set; }

    [Required]
    public DateTime EventDate { get; set; }

    /// <example>Johannesburg, Gauteng</example>
    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <example>10</example>
    [Range(0.1, 500)]
    public decimal DistanceKm { get; set; }

    /// <summary>"Run", "Walk" or "Cycle".</summary>
    /// <example>Run</example>
    [Required]
    [RegularExpression("^(Run|Walk|Cycle)$", ErrorMessage = "EventType must be Run, Walk or Cycle.")]
    public string EventType { get; set; } = string.Empty;

    /// <summary>"Draft", "Published" or "Closed". Defaults to Draft on create.</summary>
    /// <example>Published</example>
    [RegularExpression("^(Draft|Published|Closed)$")]
    public string? Status { get; set; }
}

public class EventResponse
{
    public int EventId { get; set; }
    public int OrganiserId { get; set; }
    public string? OrganiserName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public List<CategoryResponse> Categories { get; set; } = new();

    public static EventResponse From(Event e, bool includeCategories = false) => new()
    {
        EventId = e.EventId,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser?.FullName,
        Name = e.Name,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventType = e.EventType,
        Status = e.Status,
        Categories = includeCategories && e.Categories != null
            ? e.Categories.Select(CategoryResponse.From).ToList()
            : new List<CategoryResponse>()
    };
}
