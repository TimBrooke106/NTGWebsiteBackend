namespace SkipHire.Api.Models;

public class Booking
{
    public int Id { get; set; }

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Address { get; set; } = "";

    public string SkipSize { get; set; } = "";
    public string MaterialType { get; set; } = "";

    // Store date-only (best for "per day")
    public DateOnly PreferredDate { get; set; }

    // "08:00", "09:00", etc
    public string TimeSlot { get; set; } = "";

    public string? Notes { get; set; }

    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public string Status { get; set; } = "Pending"; // Pending | Confirmed | Rejected

}
