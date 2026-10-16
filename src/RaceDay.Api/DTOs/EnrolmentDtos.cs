using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

/// <summary>
/// Returned when a Participant enrols. Shows the full link between
/// the participant, the event and the selected category.
/// </summary>
public class EnrolmentResponse
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public string? ParticipantName { get; set; }
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public int EventId { get; set; }
    public string? EventName { get; set; }
    public DateTime? EventDate { get; set; }
    public string BibNumber { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public static EnrolmentResponse From(Enrolment e) => new()
    {
        EnrolmentId = e.EnrolmentId,
        ParticipantId = e.ParticipantId,
        ParticipantName = e.Participant?.FullName,
        CategoryId = e.CategoryId,
        CategoryName = e.Category?.Name,
        EventId = e.Category?.EventId ?? 0,
        EventName = e.Category?.Event?.Name,
        EventDate = e.Category?.Event?.EventDate,
        BibNumber = e.BibNumber,
        EnrolmentDate = e.EnrolmentDate,
        Status = e.Status
    };
}
