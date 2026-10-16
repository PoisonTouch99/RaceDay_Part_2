/* ============================================================
   RaceDay Database Schema
   SQL Server (SSMS) - creates schema + seeds sample data
   This script matches RaceDay_ERD.png and the EF Core Code-First
   model in src/RaceDay.Api/Data/RaceDayDbContext.cs exactly.
   ============================================================ */

/* ------------------------------------------------------------
   Clean slate: drop the ENTIRE database if it already exists,
   rather than dropping individual tables.
   Dropping table-by-table breaks if the database contains any
   leftover object from an earlier run (e.g. an old table that
   still has a foreign key into Users) - dropping the whole
   database sidesteps that entirely and guarantees a truly clean
   run on "a clean SQL Server instance", as the brief requires.

   NOTE: if the RaceDay API is currently running (e.g. via
   `dotnet run` or from Visual Studio), stop it first - an open
   connection will block SINGLE_USER mode below.
   ------------------------------------------------------------ */
USE master;
GO

IF DB_ID('RaceDayDB') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDB;
END
GO

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO

/* ------------------------------------------------------------
   Users
   Organisers and Participants share this table, separated by Role.
   Passwords are stored only as a PBKDF2 hash, never in plain text.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Users (
    UserId          INT IDENTITY(1,1) PRIMARY KEY,
    FullName        NVARCHAR(100)   NOT NULL,
    Email           NVARCHAR(150)   NOT NULL UNIQUE,
    PasswordHash    NVARCHAR(255)   NOT NULL,
    Role            NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant')),
    Phone           NVARCHAR(20)    NULL,
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE()
);
GO

/* ------------------------------------------------------------
   Events  (1 Organiser -> M Events)
   Captures name, description, date, location, distance and type.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Events (
    EventId         INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserId     INT             NOT NULL,
    Name            NVARCHAR(150)   NOT NULL,
    Description     NVARCHAR(1000)  NULL,
    EventDate       DATE            NOT NULL,
    Location        NVARCHAR(150)   NOT NULL,
    DistanceKm      DECIMAL(5,2)    NOT NULL,
    EventType       NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Events_Type CHECK (EventType IN ('Run', 'Walk', 'Cycle')),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Draft'
                        CONSTRAINT CK_Events_Status CHECK (Status IN ('Draft', 'Published', 'Closed')),
    CreatedAt       DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserId)
        REFERENCES dbo.Users(UserId)
);
GO

/* ------------------------------------------------------------
   Categories  (1 Event -> M Categories)
   Supports both age categories (Under 20, Senior) and
   distance categories (10km, 21km) via CategoryType.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Categories (
    CategoryId      INT IDENTITY(1,1) PRIMARY KEY,
    EventId         INT             NOT NULL,
    Name            NVARCHAR(100)   NOT NULL,
    CategoryType    NVARCHAR(20)    NOT NULL
                        CONSTRAINT CK_Categories_Type CHECK (CategoryType IN ('Age', 'Distance')),
    DistanceKm      DECIMAL(5,2)    NULL,   -- set for Distance categories
    MinAge          INT             NULL,   -- set for Age categories
    MaxAge          INT             NULL,   -- set for Age categories
    MaxParticipants INT             NOT NULL DEFAULT 100,
    EntryFee        DECIMAL(8,2)    NOT NULL DEFAULT 0,
    CONSTRAINT FK_Categories_Events FOREIGN KEY (EventId)
        REFERENCES dbo.Events(EventId) ON DELETE CASCADE
);
GO

/* ------------------------------------------------------------
   Enrolments
   Junction table resolving the many-to-many between Participants
   and Categories, with its own attributes.
   ------------------------------------------------------------ */
CREATE TABLE dbo.Enrolments (
    EnrolmentId     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantId   INT             NOT NULL,
    CategoryId      INT             NOT NULL,
    BibNumber       NVARCHAR(10)    NOT NULL UNIQUE,
    EnrolmentDate   DATETIME        NOT NULL DEFAULT GETDATE(),
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Pending'
                        CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Cancelled')),
    CONSTRAINT FK_Enrolments_Users FOREIGN KEY (ParticipantId)
        REFERENCES dbo.Users(UserId),
    CONSTRAINT FK_Enrolments_Categories FOREIGN KEY (CategoryId)
        REFERENCES dbo.Categories(CategoryId),
    -- A participant cannot enrol twice in the same category
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantId, CategoryId)
);
GO

/* ------------------------------------------------------------
   Results  (1-to-1 with Enrolments)
   ------------------------------------------------------------ */
CREATE TABLE dbo.Results (
    ResultId        INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT             NOT NULL UNIQUE,
    FinishTime      TIME            NULL,
    Position        INT             NULL,
    Status          NVARCHAR(20)    NOT NULL DEFAULT 'Finished'
                        CONSTRAINT CK_Results_Status CHECK (Status IN ('Finished', 'DNF', 'DSQ')),
    RecordedAt      DATETIME        NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Results_Enrolments FOREIGN KEY (EnrolmentId)
        REFERENCES dbo.Enrolments(EnrolmentId) ON DELETE CASCADE
);
GO

/* ------------------------------------------------------------
   Payments  (1-to-1 with Enrolments)
   ------------------------------------------------------------ */
CREATE TABLE dbo.Payments (
    PaymentId       INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentId     INT             NOT NULL UNIQUE,
    Amount          DECIMAL(8,2)    NOT NULL,
    PaymentDate     DATETIME        NOT NULL DEFAULT GETDATE(),
    PaymentStatus   NVARCHAR(20)    NOT NULL DEFAULT 'Pending'
                        CONSTRAINT CK_Payments_Status CHECK (PaymentStatus IN ('Pending', 'Paid', 'Refunded')),
    CONSTRAINT FK_Payments_Enrolments FOREIGN KEY (EnrolmentId)
        REFERENCES dbo.Enrolments(EnrolmentId) ON DELETE CASCADE
);
GO

/* ============================================================
   SEED DATA
   Mirrors DbSeeder.cs in the API project.
   2 Organisers, 2 Participants, 3 Events, categories per event,
   sample enrolments, a result and payments.
   NOTE: PasswordHash values below are placeholders. The API
   generates real PBKDF2 hashes; all seeded accounts in the
   running application use the password "Passw0rd!".
   ============================================================ */

-- Organisers
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role, Phone) VALUES
('Sarah Nkosi', 'sarah.nkosi@raceday.co.za', 'PBKDF2_HASH_PLACEHOLDER_1', 'Organiser', '0821234567'),
('David Botha', 'david.botha@raceday.co.za', 'PBKDF2_HASH_PLACEHOLDER_2', 'Organiser', '0839876543');

-- Participants
INSERT INTO dbo.Users (FullName, Email, PasswordHash, Role, Phone) VALUES
('Thabo Mokoena', 'thabo.mokoena@example.com', 'PBKDF2_HASH_PLACEHOLDER_3', 'Participant', '0731112222'),
('Emma van Wyk',  'emma.vanwyk@example.com',   'PBKDF2_HASH_PLACEHOLDER_4', 'Participant', '0723334444');

-- Events (1 & 2 -> Sarah; 3 -> David). Covers all three event types.
INSERT INTO dbo.Events (OrganiserId, Name, Description, EventDate, Location, DistanceKm, EventType, Status) VALUES
(1, 'Johannesburg City Run',     'Annual road race through the Johannesburg CBD.',   '2026-10-10', 'Johannesburg, Gauteng',   10.00, 'Run',   'Published'),
(1, 'Sandton Family Walk',       'A family-friendly fun walk through Sandton.',      '2026-11-15', 'Sandton, Gauteng',         5.00, 'Walk',  'Published'),
(2, 'Cape Town Coastal Cycle',   'Scenic cycle race along the Cape Town coastline.', '2026-09-20', 'Cape Town, Western Cape', 42.20, 'Cycle', 'Published');

-- Categories: a mix of distance and age categories across the three events
INSERT INTO dbo.Categories (EventId, Name, CategoryType, DistanceKm, MinAge, MaxAge, MaxParticipants, EntryFee) VALUES
(1, '5km',        'Distance',  5.00, NULL, NULL, 200, 100.00),
(1, '10km',       'Distance', 10.00, NULL, NULL, 150, 150.00),
(1, 'Under 20',   'Age',       NULL,    0,   19, 100,  80.00),
(2, '5km Family', 'Distance',  5.00, NULL, NULL, 300,  80.00),
(2, 'Senior',     'Age',       NULL,   60,  120, 100,  60.00),
(3, '21km',       'Distance', 21.10, NULL, NULL, 500, 250.00),
(3, '42km',       'Distance', 42.20, NULL, NULL, 500, 350.00);

-- Enrolments
INSERT INTO dbo.Enrolments (ParticipantId, CategoryId, BibNumber, Status) VALUES
(3, 1, 'BIB0001', 'Confirmed'),  -- Thabo -> 5km (Jhb City Run)
(3, 6, 'BIB0002', 'Confirmed'),  -- Thabo -> 21km (Cape Town Cycle)
(4, 2, 'BIB0003', 'Confirmed'),  -- Emma  -> 10km (Jhb City Run)
(4, 4, 'BIB0004', 'Pending');    -- Emma  -> 5km Family (Sandton Walk)

-- Results (one recorded finish)
INSERT INTO dbo.Results (EnrolmentId, FinishTime, Position, Status) VALUES
(1, '00:26:14', 12, 'Finished');

-- Payments
INSERT INTO dbo.Payments (EnrolmentId, Amount, PaymentStatus) VALUES
(1, 100.00, 'Paid'),
(2, 250.00, 'Paid'),
(3, 150.00, 'Paid'),
(4,  80.00, 'Pending');
GO

/* ------------------------------------------------------------
   Verification query - proves the relationships resolve correctly
   ------------------------------------------------------------ */
SELECT
    e.Name          AS EventName,
    e.EventType,
    c.Name          AS CategoryName,
    c.CategoryType,
    u.FullName      AS Participant,
    en.BibNumber,
    en.Status       AS EnrolmentStatus,
    r.FinishTime,
    r.Position
FROM dbo.Enrolments en
JOIN dbo.Users u       ON u.UserId     = en.ParticipantId
JOIN dbo.Categories c  ON c.CategoryId = en.CategoryId
JOIN dbo.Events e      ON e.EventId    = c.EventId
LEFT JOIN dbo.Results r ON r.EnrolmentId = en.EnrolmentId
ORDER BY e.EventDate, c.Name;
GO
