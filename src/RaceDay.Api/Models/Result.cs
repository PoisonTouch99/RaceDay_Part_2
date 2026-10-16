using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>Finish time and position for a single enrolment. One-to-one with Enrolment.</summary>
public class Result
{
    public int ResultId { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    public TimeSpan? FinishTime { get; set; }

    public int? Position { get; set; }

    /// <summary>"Finished", "DNF" (did not finish) or "DSQ" (disqualified).</summary>
    [Required, MaxLength(20)]
    public string Status { get; set; } = ResultStatuses.Finished;

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public static class ResultStatuses
{
    public const string Finished = "Finished";
    public const string DNF = "DNF";
    public const string DSQ = "DSQ";
    public static readonly string[] All = { Finished, DNF, DSQ };
}
