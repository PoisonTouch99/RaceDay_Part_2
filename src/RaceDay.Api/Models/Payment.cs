using System.ComponentModel.DataAnnotations;

namespace RaceDay.Api.Models;

/// <summary>Entry-fee payment for an enrolment. One-to-one with Enrolment.</summary>
public class Payment
{
    public int PaymentId { get; set; }

    public int EnrolmentId { get; set; }
    public Enrolment? Enrolment { get; set; }

    public decimal Amount { get; set; }

    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

    /// <summary>"Pending", "Paid" or "Refunded".</summary>
    [Required, MaxLength(20)]
    public string PaymentStatus { get; set; } = PaymentStatuses.Pending;
}

public static class PaymentStatuses
{
    public const string Pending = "Pending";
    public const string Paid = "Paid";
    public const string Refunded = "Refunded";
}
