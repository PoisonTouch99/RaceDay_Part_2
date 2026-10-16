using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using RaceDay.Api.Data;
using RaceDay.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------- Database (EF Core, Code-First) ----------
builder.Services.AddDbContext<RaceDayDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ---------- Services ----------
builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();

// ---------- Session-based authentication ----------
// The session stores the authenticated user's id and role server-side;
// the client only ever holds an opaque session cookie.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".RaceDay.Session";
    options.Cookie.HttpOnly = true;                       // not readable from JavaScript
    options.Cookie.IsEssential = true;                    // required for the app to function
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.IdleTimeout = TimeSpan.FromMinutes(60);
});
builder.Services.AddHttpContextAccessor();

// ---------- Controllers & Swagger ----------
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "RaceDay API",
        Version = "v1",
        Description =
            "RESTful API for the RaceDay event management system.\n\n" +
            "**Roles:** Organiser (manages events, categories and results) and " +
            "Participant (browses events, enrols, views own results).\n\n" +
            "**Authentication:** session based. Call `POST /api/auth/login` first — the browser " +
            "stores the session cookie automatically, and subsequent calls from this page are " +
            "authenticated. Use `POST /api/auth/logout` to clear it."
    });

    // Pull the <summary> / <remarks> / <response> XML comments into the Swagger UI
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
});

var app = builder.Build();

// ---------- Apply migrations and seed on startup ----------
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<RaceDayDbContext>();
    db.Database.Migrate();
    DbSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>());
}

// ---------- Pipeline ----------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(o =>
    {
        o.SwaggerEndpoint("/swagger/v1/swagger.json", "RaceDay API v1");
        o.DocumentTitle = "RaceDay API";
        o.RoutePrefix = "swagger";   // available at /swagger
    });
}

app.UseHttpsRedirection();

// Session must come before authorization filters that read it.
app.UseSession();

app.MapControllers();

app.Run();

/// <summary>Exposed so the integration test project can reference the entry point.</summary>
public partial class Program { }
