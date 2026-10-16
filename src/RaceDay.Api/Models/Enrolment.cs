using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>
/// Junction entity resolving the many-to-many between Participants and Categories.
/// Records the link between the Participant, the event (via Category) and the chosen category.
/// </summary>
public class Enrolment
{
    public int EnrolmentId { get; set; }

    public int ParticipantId { get; set; }
    public User? Participant { get; set; }

    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    [Required, MaxLength(10)]
    public string BibNumber { get; set; } = string.Empty;

    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;

    /// <summary>"Pending", "Confirmed" or "Cancelled".</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = EnrolmentStatuses.Pending;

    public Result? Result { get; set; }
    public Payment? Payment { get; set; }
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";
}
