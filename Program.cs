using Microsoft.EntityFrameworkCore;
using Resend;
using SkipHire.Api.Controllers;
using SkipHire.Api.Data;
using SkipHire.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Controllers / Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        // Allow local dev + your production Netlify site (+ any netlify deploy previews)
        policy.SetIsOriginAllowed(origin =>
                origin == "http://localhost:4200" ||
                origin == "https://ntgexcavations.netlify.app" ||
                origin.EndsWith(".netlify.app"))
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Email + DB + Auth settings
builder.Services.AddHttpClient();
builder.Services.AddScoped<IEmailSender, ResendEmailSender>();

var app = builder.Build();


// Apply EF migrations on startup (creates tables like Bookings)
using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        logger.LogInformation("Applying migrations...");
        db.Database.Migrate();
        logger.LogInformation("✅ Migrations applied OK");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Migration failed");
        throw;
    }
}

// Swagger only in Development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Basic health endpoints
app.MapGet("/", () => Results.Ok("NTG Backend is running ✅"));
app.MapGet("/health", () => Results.Ok("OK"));

// Middleware order matters
app.UseCors("AllowedOrigins");
app.UseAuthorization();

app.MapControllers();

app.Run();
