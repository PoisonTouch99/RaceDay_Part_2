using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>
/// An age or distance category within an event (e.g. "Under 20", "Senior", "10km", "21km").
/// </summary>
public class Category
{
    public int CategoryId { get; set; }

    public int EventId { get; set; }
    public Event? Event { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Age" or "Distance" - distinguishes the two kinds of category.</summary>
    [Required, MaxLength(20)]
    public string CategoryType { get; set; } = CategoryTypes.Distance;

    /// <summary>Set for distance categories; null for age categories.</summary>
    public decimal? DistanceKm { get; set; }

    /// <summary>Set for age categories; null for distance categories.</summary>
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    public int MaxParticipants { get; set; } = 100;

    public decimal EntryFee { get; set; }

    public ICollection<Enrolment> Enrolments { get; set; } = new List<Enrolment>();
}

public static class CategoryTypes
{
    public const string Age = "Age";
    public const string Distance = "Distance";
    public static readonly string[] All = { Age, Distance };
}
