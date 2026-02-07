using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SkipHire.Api.Data;
using SkipHire.Api.Models;
using SkipHire.Api.Services;

namespace SkipHire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;

    public BookingsController(
        AppDbContext db,
        IEmailSender emailSender,
        IOptions<EmailSettings> emailSettings)
    {
        _db = db;
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
    }

    /* ============================
       Admin helpers
       ============================ */

    private bool IsAdmin()
    {
        var token = Request.Headers["X-Admin-Token"].ToString();
        return AdminController.IsValidToken(token);
    }

    /* ============================
       Public endpoints
       ============================ */

    // GET api/bookings/slots?date=YYYY-MM-DD
    [HttpGet("slots")]
    public async Task<IActionResult> GetBookedSlots([FromQuery] string date)
    {
        try
        {
            if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d))
                return BadRequest(new { message = "Invalid date format. Use YYYY-MM-DD." });

            var booked = await _db.Bookings
                .Where(b => b.PreferredDate == d)
                .Select(b => b.TimeSlot)
                .ToListAsync();

            return Ok(booked);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return StatusCode(500, new { message = "Failed to fetch booked slots." });
        }
    }

    // POST api/bookings
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.FirstName) ||
            string.IsNullOrWhiteSpace(req.Email))
            return BadRequest(new { message = "Missing required fields." });

        if (!DateOnly.TryParseExact(req.PreferredDate, "yyyy-MM-dd", out var dateOnly))
            return BadRequest(new { message = "PreferredDate must be YYYY-MM-DD." });

        var allowedSlots = new[] { "08:00", "09:00", "10:00", "11:00", "12:00" };
        if (!allowedSlots.Contains(req.TimeSlot))
            return BadRequest(new { message = "Invalid time slot." });

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (dateOnly < today)
            return BadRequest(new { message = "PreferredDate cannot be in the past." });

        if (dateOnly.DayOfWeek == DayOfWeek.Sunday)
            return BadRequest(new { message = "Bookings are not available on Sundays." });

        if (dateOnly == today && DateTime.UtcNow.TimeOfDay >= new TimeSpan(12, 0, 0))
            return BadRequest(new { message = "Same-day bookings close at 12:00." });

        var booking = new Booking
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName?.Trim(),
            Email = req.Email.Trim(),
            Mobile = req.Mobile?.Trim(),
            Address = req.Address?.Trim(),
            PreferredDate = dateOnly,
            TimeSlot = req.TimeSlot,
            Notes = req.Notes,
            CreatedUtc = DateTime.UtcNow,
            Status = "Pending"
        };

        _db.Bookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new
            {
                message = "That time slot is already booked for this date."
            });
        }

        // Emails must NEVER block a booking
        try
        {
            var fullName = $"{booking.FirstName} {booking.LastName}".Trim();

            var detailsHtml = $@"
                <h2>Skip Booking Details</h2>
                <p><b>Name:</b> {fullName}</p>
                <p><b>Email:</b> {booking.Email}</p>
                <p><b>Mobile:</b> {booking.Mobile}</p>
                <p><b>Address:</b> {booking.Address}</p>
                <p><b>Preferred Date:</b> {booking.PreferredDate:dddd, dd MMM yyyy}</p>
                <p><b>Preferred Time:</b> {booking.TimeSlot}</p>
                <p><b>Notes:</b> {System.Net.WebUtility.HtmlEncode(booking.Notes ?? "")}</p>
            ";

            await _emailSender.SendAsync(
                booking.Email,
                "Your Skip Booking Request - NTG Excavations",
                $"<p>Thanks for your booking request.</p>{detailsHtml}"
            );

            await _emailSender.SendAsync(
                _emailSettings.AdminEmail,
                $"NEW Booking - {fullName}",
                detailsHtml
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine("Email failed:");
            Console.WriteLine(ex);
        }

        return Ok(new { message = "Booking submitted successfully." });
    }

    /* ============================
       Admin endpoints
       ============================ */

    // GET api/bookings/admin
    [HttpGet("admin")]
    public async Task<IActionResult> GetAllForAdmin()
    {
        if (!IsAdmin()) return Unauthorized();

        var items = await _db.Bookings
            .OrderByDescending(b => b.CreatedUtc)
            .Select(b => new
            {
                b.Id,
                b.FirstName,
                b.LastName,
                b.Email,
                b.Mobile,
                b.Address,
                PreferredDate = b.PreferredDate.ToString("yyyy-MM-dd"),
                b.TimeSlot,
                b.Notes,
                b.CreatedUtc,
                b.Status
            })
            .ToListAsync();

        return Ok(items);
    }

    // PUT api/bookings/admin/{id}/status
    [HttpPut("admin/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateBookingStatusRequest req)
    {
        if (!IsAdmin()) return Unauthorized();

        var allowed = new[] { "Pending", "Confirmed", "Rejected" };
        if (!allowed.Contains(req.Status))
            return BadRequest(new { message = "Invalid status." });

        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        booking.Status = req.Status;
        await _db.SaveChangesAsync();

        return Ok(new { booking.Id, booking.Status });
    }

    /* ============================
       Debug (temporary)
       ============================ */

    [HttpGet("debug/count")]
    public async Task<IActionResult> DebugCount()
    {
        var count = await _db.Bookings.CountAsync();
        return Ok(new { count });
    }
}
