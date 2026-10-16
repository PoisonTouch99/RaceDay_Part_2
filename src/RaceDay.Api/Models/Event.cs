using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>A race event owned by a single Organiser.</summary>
public class Event
{
    public int EventId { get; set; }

    /// <summary>FK to the Organiser (User) who owns this event.</summary>
    public int OrganiserId { get; set; }
    public User? Organiser { get; set; }

    [Required, MaxLength(150)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    public DateTime EventDate { get; set; }

    [Required, MaxLength(150)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Headline distance for the event in kilometres.</summary>
    public decimal DistanceKm { get; set; }

    /// <summary>"Run", "Walk" or "Cycle".</summary>
    [Required, MaxLength(20)]
    public string EventType { get; set; } = string.Empty;

    /// <summary>"Draft", "Published" or "Closed".</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = EventStatuses.Draft;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Category> Categories { get; set; } = new List<Category>();
}

public static class EventTypes
{
    public const string Run = "Run";
    public const string Walk = "Walk";
    public const string Cycle = "Cycle";
    public static readonly string[] All = { Run, Walk, Cycle };
}

public static class EventStatuses
{
    public const string Draft = "Draft";
    public const string Published = "Published";
    public const string Closed = "Closed";
    public static readonly string[] All = { Draft, Published, Closed };
}
