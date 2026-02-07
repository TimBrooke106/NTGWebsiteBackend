using Microsoft.EntityFrameworkCore;
using SkipHire.Api.Data;
using SkipHire.Api.Services;
using SkipHire.Api.Controllers;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowedOrigins", policy =>
    {
        policy.WithOrigins(
                "http://localhost:4200",
                "http://ntgexcavations.com"
            )
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

builder.Services.AddHttpClient();
builder.Services.AddScoped<IEmailSender, ResendEmailSender>();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.Configure<AdminAuthSettings>(builder.Configuration.GetSection("AdminAuth"));

var app = builder.Build();

// ✅ apply migrations on startup
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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/", () => Results.Ok("NTG Backend is running ✅"));
app.MapGet("/health", () => Results.Ok("OK"));

app.UseCors("AllowedOrigins");
app.UseAuthorization();
app.MapControllers();
app.Run();
