using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.DTOs;

public class ResultRequest
{
    /// <summary>Finish time as hh:mm:ss. Leave null for a DNF or DSQ.</summary>
    /// <example>00:52:14</example>
    public TimeSpan? FinishTime { get; set; }

    /// <summary>Finishing position within the category.</summary>
    /// <example>12</example>
    [Range(1, 1000000)]
    public int? Position { get; set; }

    /// <summary>"Finished", "DNF" or "DSQ".</summary>
    /// <example>Finished</example>
    [Required]
    [RegularExpression("^(Finished|DNF|DSQ)$", ErrorMessage = "Status must be Finished, DNF or DSQ.")]
    public string Status { get; set; } = ResultStatuses.Finished;
}

public class ResultResponse
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public string? ParticipantName { get; set; }
    public string BibNumber { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string? EventName { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime RecordedAt { get; set; }

    public static ResultResponse From(Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        ParticipantName = r.Enrolment?.Participant?.FullName,
        BibNumber = r.Enrolment?.BibNumber ?? string.Empty,
        CategoryId = r.Enrolment?.CategoryId ?? 0,
        CategoryName = r.Enrolment?.Category?.Name,
        EventName = r.Enrolment?.Category?.Event?.Name,
        FinishTime = r.FinishTime,
        Position = r.Position,
        Status = r.Status,
        RecordedAt = r.RecordedAt
    };
}
