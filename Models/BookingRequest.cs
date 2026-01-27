namespace SkipHire.Api.Models;

public class BookingRequest
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public string Mobile { get; set; } = "";
    public string Address { get; set; } = "";
    public string SkipSize { get; set; } = "";
    public string MaterialType { get; set; } = "";

    // Expect: "YYYY-MM-DD"
    public string PreferredDate { get; set; } = "";

    // Expect: "08:00" etc
    public string TimeSlot { get; set; } = "";

    public string? Notes { get; set; }
}
