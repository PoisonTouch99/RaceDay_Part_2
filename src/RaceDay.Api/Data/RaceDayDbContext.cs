using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data;

/// <summary>
/// EF Core Code-First context. The model configured here matches the Part 1 ERD
/// and the SQL script in /docs exactly.
/// </summary>
public class RaceDayDbContext : DbContext
{
    public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Enrolment> Enrolments => Set<Enrolment>();
    public DbSet<Result> Results => Set<Result>();
    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Check constraints and SQL default values are relational-only features.
        // The unit tests run on the InMemory provider, which throws if they are configured,
        // so they are applied only when running against a relational database (SQL Server).
        var isRelational = Database.IsRelational();

        // ---------- Users ----------
        b.Entity<User>(e =>
        {
            e.HasKey(x => x.UserId);
            e.HasIndex(x => x.Email).IsUnique();                 // UNIQUE constraint
            if (isRelational) e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");
            if (isRelational) e.ToTable(t => t.HasCheckConstraint(
                "CK_Users_Role", "[Role] IN ('Organiser','Participant')"));
        });

        // ---------- Events ----------
        b.Entity<Event>(e =>
        {
            e.HasKey(x => x.EventId);
            if (isRelational) e.Property(x => x.DistanceKm).HasColumnType("decimal(5,2)");
            e.Property(x => x.Status).HasDefaultValue(EventStatuses.Draft);
            if (isRelational) e.Property(x => x.CreatedAt).HasDefaultValueSql("GETDATE()");

            // 1 Organiser -> M Events. Restrict so an organiser with events can't be silently deleted.
            e.HasOne(x => x.Organiser)
             .WithMany(u => u.OrganisedEvents)
             .HasForeignKey(x => x.OrganiserId)
             .OnDelete(DeleteBehavior.Restrict);

            if (isRelational) e.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Events_Status", "[Status] IN ('Draft','Published','Closed')");
                t.HasCheckConstraint("CK_Events_Type", "[EventType] IN ('Run','Walk','Cycle')");
            });
        });

        // ---------- Categories ----------
        b.Entity<Category>(e =>
        {
            e.HasKey(x => x.CategoryId);
            if (isRelational) e.Property(x => x.DistanceKm).HasColumnType("decimal(5,2)");
            if (isRelational) e.Property(x => x.EntryFee).HasColumnType("decimal(8,2)");
            e.Property(x => x.MaxParticipants).HasDefaultValue(100);

            // 1 Event -> M Categories. Cascade: deleting an event removes its categories.
            e.HasOne(x => x.Event)
             .WithMany(ev => ev.Categories)
             .HasForeignKey(x => x.EventId)
             .OnDelete(DeleteBehavior.Cascade);

            if (isRelational) e.ToTable(t => t.HasCheckConstraint(
                "CK_Categories_Type", "[CategoryType] IN ('Age','Distance')"));
        });

        // ---------- Enrolments ----------
        b.Entity<Enrolment>(e =>
        {
            e.HasKey(x => x.EnrolmentId);
            e.HasIndex(x => x.BibNumber).IsUnique();
            // A participant cannot enrol twice in the same category.
            e.HasIndex(x => new { x.ParticipantId, x.CategoryId }).IsUnique();
            if (isRelational) e.Property(x => x.EnrolmentDate).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.Status).HasDefaultValue(EnrolmentStatuses.Pending);

            e.HasOne(x => x.Participant)
             .WithMany(u => u.Enrolments)
             .HasForeignKey(x => x.ParticipantId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Category)
             .WithMany(c => c.Enrolments)
             .HasForeignKey(x => x.CategoryId)
             .OnDelete(DeleteBehavior.Restrict);

            if (isRelational) e.ToTable(t => t.HasCheckConstraint(
                "CK_Enrolments_Status", "[Status] IN ('Pending','Confirmed','Cancelled')"));
        });

        // ---------- Results (1-to-1 with Enrolment) ----------
        b.Entity<Result>(e =>
        {
            e.HasKey(x => x.ResultId);
            e.HasIndex(x => x.EnrolmentId).IsUnique();           // enforces 1-to-1
            e.Property(x => x.Status).HasDefaultValue(ResultStatuses.Finished);
            if (isRelational) e.Property(x => x.RecordedAt).HasDefaultValueSql("GETDATE()");

            e.HasOne(x => x.Enrolment)
             .WithOne(en => en.Result)
             .HasForeignKey<Result>(x => x.EnrolmentId)
             .OnDelete(DeleteBehavior.Cascade);

            if (isRelational) e.ToTable(t => t.HasCheckConstraint(
                "CK_Results_Status", "[Status] IN ('Finished','DNF','DSQ')"));
        });

        // ---------- Payments (1-to-1 with Enrolment) ----------
        b.Entity<Payment>(e =>
        {
            e.HasKey(x => x.PaymentId);
            e.HasIndex(x => x.EnrolmentId).IsUnique();           // enforces 1-to-1
            if (isRelational) e.Property(x => x.Amount).HasColumnType("decimal(8,2)");
            if (isRelational) e.Property(x => x.PaymentDate).HasDefaultValueSql("GETDATE()");
            e.Property(x => x.PaymentStatus).HasDefaultValue(PaymentStatuses.Pending);

            e.HasOne(x => x.Enrolment)
             .WithOne(en => en.Payment)
             .HasForeignKey<Payment>(x => x.EnrolmentId)
             .OnDelete(DeleteBehavior.Cascade);

            if (isRelational) e.ToTable(t => t.HasCheckConstraint(
                "CK_Payments_Status", "[PaymentStatus] IN ('Pending','Paid','Refunded')"));
        });
    }
}
