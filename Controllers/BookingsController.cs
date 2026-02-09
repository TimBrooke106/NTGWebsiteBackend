using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SkipHire.Api.Data;
using SkipHire.Api.Models;
using SkipHire.Api.Services;

[ApiController]
[Route("api/bookings")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _email;
    private readonly IConfiguration _config;

    public BookingsController(AppDbContext db, IEmailSender email, IConfiguration config)
    {
        _db = db;
        _email = email;
        _config = config;
    }

    // 🔐 simple admin auth
    private bool IsAdmin()
    {
        var token = Request.Headers["X-Admin-Token"].ToString();
        return token == _config["AdminAuth:Password"];
    }

    // ==========================
    // GET: admin list
    // ==========================
    [HttpGet("admin")]
    public async Task<IActionResult> GetAllAdmin()
    {
        if (!IsAdmin()) return Unauthorized();

        var bookings = await _db.Bookings
            .OrderByDescending(b => b.CreatedUtc)
            .ToListAsync();

        return Ok(bookings);
    }

    // ==========================
    // GET: admin stats
    // ==========================
    [HttpGet("admin/stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsAdmin()) return Unauthorized();

        var total = await _db.Bookings.CountAsync();
        var pending = await _db.Bookings.CountAsync(b => b.Status == "Pending");
        var confirmed = await _db.Bookings.CountAsync(b => b.Status == "Confirmed");

        return Ok(new
        {
            totalBookings = total,
            pending,
            confirmed
        });
    }

    // ==========================
    // PUT: update status
    // ==========================
    [HttpPut("admin/{id}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] StatusDto dto)
    {
        if (!IsAdmin()) return Unauthorized();

        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        // ❌ REJECT → delete booking
        if (dto.Status == "Rejected")
        {
            _db.Bookings.Remove(booking);
            await _db.SaveChangesAsync();
            return Ok();
        }

        // ✅ CONFIRM
        booking.Status = "Confirmed";
        await _db.SaveChangesAsync();

        // 📧 send confirmation email
        await _email.SendAsync(
            booking.Email,
            "Your booking has been confirmed ✅",
            $@"
                <h2>Booking Confirmed</h2>
                <p>Hi {booking.FirstName},</p>
                <p>Your booking for <b>{booking.PreferredDate:yyyy-MM-dd}</b> at <b>{booking.TimeSlot}</b> has been confirmed.</p>
                <p>Thank you,<br/>NTG Excavations</p>
            "
        );

        return Ok();
    }
}

// ==========================
// DTO
// ==========================
public class StatusDto
{
    public string Status { get; set; } = "";
}
