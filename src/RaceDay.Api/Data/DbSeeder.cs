using RaceDay.Api.Models;
using RaceDay.Api.Services;

namespace RaceDay.Api.Data;

/// <summary>
/// Seeds the same sample data as the Part 1 SQL script:
/// 2 Organisers, 2 Participants, 3 Events, categories for each event, and sample enrolments.
/// Runs only when the database is empty, so restarting the app does not duplicate rows.
/// </summary>
public static class DbSeeder
{
    public static void Seed(RaceDayDbContext dbo, IPasswordHasher hasher)
    {
        if (dbo.Users.Any()) return;

        // ---------- Users ----------
        var sarah = new User
        {
            FullName = "Sarah Nkosi",
            Email = "sarah.nkosi@raceday.co.za",
            PasswordHash = hasher.Hash("Passw0rd!"),
            Role = Roles.Organiser,
            Phone = "0821234567"
        };
        var david = new User
        {
            FullName = "David Botha",
            Email = "david.botha@raceday.co.za",
            PasswordHash = hasher.Hash("Passw0rd!"),
            Role = Roles.Organiser,
            Phone = "0839876543"
        };
        var thabo = new User
        {
            FullName = "Thabo Mokoena",
            Email = "thabo.mokoena@example.com",
            PasswordHash = hasher.Hash("Passw0rd!"),
            Role = Roles.Participant,
            Phone = "0731112222"
        };
        var emma = new User
        {
            FullName = "Emma van Wyk",
            Email = "emma.vanwyk@example.com",
            PasswordHash = hasher.Hash("Passw0rd!"),
            Role = Roles.Participant,
            Phone = "0723334444"
        };

        dbo.Users.AddRange(sarah, david, thabo, emma);
        dbo.SaveChanges();

        // ---------- Events ----------
        var jhbRun = new Event
        {
            OrganiserId = sarah.UserId,
            Name = "Johannesburg City Run",
            Description = "Annual road race through the Johannesburg CBD.",
            EventDate = new DateTime(2026, 10, 10),
            Location = "Johannesburg, Gauteng",
            DistanceKm = 10m,
            EventType = EventTypes.Run,
            Status = EventStatuses.Published
        };
        var sandtonWalk = new Event
        {
            OrganiserId = sarah.UserId,
            Name = "Sandton Family Walk",
            Description = "A family-friendly fun walk through Sandton.",
            EventDate = new DateTime(2026, 11, 15),
            Location = "Sandton, Gauteng",
            DistanceKm = 5m,
            EventType = EventTypes.Walk,
            Status = EventStatuses.Published
        };
        var capeCycle = new Event
        {
            OrganiserId = david.UserId,
            Name = "Cape Town Coastal Cycle",
            Description = "Scenic cycle race along the Cape Town coastline.",
            EventDate = new DateTime(2026, 9, 20),
            Location = "Cape Town, Western Cape",
            DistanceKm = 42.2m,
            EventType = EventTypes.Cycle,
            Status = EventStatuses.Published
        };

        dbo.Events.AddRange(jhbRun, sandtonWalk, capeCycle);
        dbo.SaveChanges();

        // ---------- Categories (a mix of distance and age categories) ----------
        var cat5km = new Category
        {
            EventId = jhbRun.EventId, Name = "5km", CategoryType = CategoryTypes.Distance,
            DistanceKm = 5m, MaxParticipants = 200, EntryFee = 100m
        };
        var cat10km = new Category
        {
            EventId = jhbRun.EventId, Name = "10km", CategoryType = CategoryTypes.Distance,
            DistanceKm = 10m, MaxParticipants = 150, EntryFee = 150m
        };
        var catUnder20 = new Category
        {
            EventId = jhbRun.EventId, Name = "Under 20", CategoryType = CategoryTypes.Age,
            MinAge = 0, MaxAge = 19, MaxParticipants = 100, EntryFee = 80m
        };
        var catFamily = new Category
        {
            EventId = sandtonWalk.EventId, Name = "5km Family", CategoryType = CategoryTypes.Distance,
            DistanceKm = 5m, MaxParticipants = 300, EntryFee = 80m
        };
        var catSenior = new Category
        {
            EventId = sandtonWalk.EventId, Name = "Senior", CategoryType = CategoryTypes.Age,
            MinAge = 60, MaxAge = 120, MaxParticipants = 100, EntryFee = 60m
        };
        var cat21km = new Category
        {
            EventId = capeCycle.EventId, Name = "21km", CategoryType = CategoryTypes.Distance,
            DistanceKm = 21.1m, MaxParticipants = 500, EntryFee = 250m
        };
        var cat42km = new Category
        {
            EventId = capeCycle.EventId, Name = "42km", CategoryType = CategoryTypes.Distance,
            DistanceKm = 42.2m, MaxParticipants = 500, EntryFee = 350m
        };

        dbo.Categories.AddRange(cat5km, cat10km, catUnder20, catFamily, catSenior, cat21km, cat42km);
        dbo.SaveChanges();

        // ---------- Enrolments ----------
        var e1 = new Enrolment
        {
            ParticipantId = thabo.UserId, CategoryId = cat5km.CategoryId,
            BibNumber = "BIB0001", Status = EnrolmentStatuses.Confirmed
        };
        var e2 = new Enrolment
        {
            ParticipantId = thabo.UserId, CategoryId = cat21km.CategoryId,
            BibNumber = "BIB0002", Status = EnrolmentStatuses.Confirmed
        };
        var e3 = new Enrolment
        {
            ParticipantId = emma.UserId, CategoryId = cat10km.CategoryId,
            BibNumber = "BIB0003", Status = EnrolmentStatuses.Confirmed
        };
        var e4 = new Enrolment
        {
            ParticipantId = emma.UserId, CategoryId = catFamily.CategoryId,
            BibNumber = "BIB0004", Status = EnrolmentStatuses.Pending
        };

        dbo.Enrolments.AddRange(e1, e2, e3, e4);
        dbo.SaveChanges();

        // ---------- Results ----------
        dbo.Results.Add(new Result
        {
            EnrolmentId = e1.EnrolmentId,
            FinishTime = new TimeSpan(0, 26, 14),
            Position = 12,
            Status = ResultStatuses.Finished
        });

        // ---------- Payments ----------
        dbo.Payments.AddRange(
            new Payment { EnrolmentId = e1.EnrolmentId, Amount = 100m, PaymentStatus = PaymentStatuses.Paid },
            new Payment { EnrolmentId = e2.EnrolmentId, Amount = 250m, PaymentStatus = PaymentStatuses.Paid },
            new Payment { EnrolmentId = e3.EnrolmentId, Amount = 150m, PaymentStatus = PaymentStatuses.Paid },
            new Payment { EnrolmentId = e4.EnrolmentId, Amount = 80m, PaymentStatus = PaymentStatuses.Pending });

        dbo.SaveChanges();
    }
}
