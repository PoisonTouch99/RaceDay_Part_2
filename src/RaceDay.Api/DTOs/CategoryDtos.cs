using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class CategoryRequest
{
    /// <example>Under 20</example>
    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>"Age" or "Distance".</summary>
    /// <example>Age</example>
    [Required]
    [RegularExpression("^(Age|Distance)$", ErrorMessage = "CategoryType must be 'Age' or 'Distance'.")]
    public string CategoryType { get; set; } = string.Empty;

    /// <summary>Required for Distance categories, ignored for Age categories.</summary>
    /// <example>10</example>
    public decimal? DistanceKm { get; set; }

    /// <summary>Required for Age categories, ignored for Distance categories.</summary>
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }

    /// <example>150</example>
    [Range(1, 100000)]
    public int MaxParticipants { get; set; } = 100;

    /// <example>150.00</example>
    [Range(0, 100000)]
    public decimal EntryFee { get; set; }
}

public class CategoryResponse
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }
    public int EnrolledCount { get; set; }

    public static CategoryResponse From(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        EventName = c.Event?.Name,
        Name = c.Name,
        CategoryType = c.CategoryType,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        MaxParticipants = c.MaxParticipants,
        EntryFee = c.EntryFee,
        EnrolledCount = c.Enrolments?.Count(e => e.Status != EnrolmentStatuses.Cancelled) ?? 0
    };
}
