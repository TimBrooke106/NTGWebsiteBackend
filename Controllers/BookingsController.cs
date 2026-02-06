using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SkipHire.Api.Models;
using SkipHire.Api.Services;
using Microsoft.EntityFrameworkCore;
using SkipHire.Api.Data;
using SkipHire.Api.Controllers; // for AdminController.IsValidToken
using SkipHire.Api.Data;
using Microsoft.EntityFrameworkCore;



namespace SkipHire.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BookingsController : ControllerBase
{
    private readonly IEmailSender _emailSender;
    private readonly EmailSettings _emailSettings;

    private readonly AppDbContext _db;

    private bool IsAdmin()
    {
        var token = Request.Headers["X-Admin-Token"].ToString();
        return AdminController.IsValidToken(token);
    }

    [HttpGet("admin")]
    public async Task<IActionResult> GetAllForAdmin([FromServices] AppDbContext db)
    {
        if (!IsAdmin()) return Unauthorized();

        var items = await db.Bookings
            .OrderByDescending(b => b.CreatedUtc)
            .Select(b => new {
                b.Id,
                b.FirstName,
                b.LastName,
                b.Email,
                b.Mobile,
                b.Address,
                b.SkipSize,
                b.MaterialType,
                PreferredDate = b.PreferredDate.ToString("yyyy-MM-dd"),
                b.TimeSlot,
                b.Notes,
                b.CreatedUtc,
                Status = b.Status
            })
            .ToListAsync();

        return Ok(items);
    }

    [HttpGet("admin/stats")]
    public async Task<IActionResult> GetStats([FromServices] AppDbContext db)
    {
        if (!IsAdmin()) return Unauthorized();

        var total = await db.Bookings.CountAsync();
        var pending = await db.Bookings.CountAsync(b => b.Status == "Pending");
        var confirmed = await db.Bookings.CountAsync(b => b.Status == "Confirmed");

        return Ok(new
        {
            totalBookings = total,
            pending,
            confirmed
        });
    }




    public BookingsController(IEmailSender emailSender, IOptions<EmailSettings> emailSettings, AppDbContext db)
    {
        _emailSender = emailSender;
        _emailSettings = emailSettings.Value;
        _db = db;
    }

    [HttpGet("slots")]
    public async Task<ActionResult<List<string>>> GetBookedSlots([FromQuery] string date)
    {
        if (!DateOnly.TryParse(date, out var d))
            return BadRequest("Invalid date format. Use YYYY-MM-DD.");

        var booked = await _db.Bookings
            .Where(b => b.PreferredDate == d)
            .Select(b => b.TimeSlot)
            .ToListAsync();

        return Ok(booked);
    }

    [HttpPut("admin/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
    int id,
    [FromBody] UpdateBookingStatusRequest req,
    [FromServices] AppDbContext db)
    {
        if (!IsAdmin()) return Unauthorized();

        var allowed = new[] { "Pending", "Confirmed", "Rejected" };
        if (!allowed.Contains(req.Status))
            return BadRequest("Invalid status.");

        var booking = await db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        booking.Status = req.Status;
        await db.SaveChangesAsync();

        return Ok(new { booking.Id, booking.Status });
    }


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.FirstName))
            return BadRequest("Missing required fields.");

        if (!DateOnly.TryParse(req.PreferredDate, out var dateOnly))
            return BadRequest("PreferredDate must be YYYY-MM-DD.");

        var allowedSlots = new[] { "08:00", "09:00", "10:00", "11:00", "12:00" };
        if (!allowedSlots.Contains(req.TimeSlot))
            return BadRequest("Invalid time slot.");

        var today = DateOnly.FromDateTime(DateTime.Now);

        if (dateOnly < today)
            return BadRequest("PreferredDate cannot be in the past.");

        if (dateOnly.DayOfWeek == DayOfWeek.Sunday)
            return BadRequest("Bookings are not available on Sundays.");

        // Disable same-day after 12:00
        if (dateOnly == today && DateTime.Now.TimeOfDay >= new TimeSpan(12, 0, 0))
            return BadRequest("Same-day bookings are not available after 12:00.");



        var booking = new Booking
        {
            FirstName = req.FirstName.Trim(),
            LastName = req.LastName.Trim(),
            Email = req.Email.Trim(),
            Mobile = req.Mobile.Trim(),
            Address = req.Address.Trim(),
            SkipSize = req.SkipSize.Trim(),
            MaterialType = req.MaterialType.Trim(),
            PreferredDate = dateOnly,
            TimeSlot = req.TimeSlot,
            Notes = req.Notes
        };

        _db.Bookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Unique constraint hit => already booked
            return Conflict(new { message = "That time slot is already booked for this date." });
        }

        // Email details include time
        var fullName = $"{req.FirstName} {req.LastName}";
        var detailsHtml = $@"
        <h2>Skip Booking Details</h2>
        <p><b>Name:</b> {fullName}</p>
        <p><b>Email:</b> {req.Email}</p>
        <p><b>Mobile:</b> {req.Mobile}</p>
        <p><b>Address:</b> {req.Address}</p>
        <p><b>Preferred Date:</b> {dateOnly:dddd, dd MMM yyyy}</p>
        <p><b>Preferred Time:</b> {req.TimeSlot}</p>
        <p><b>Additional Info:</b> {System.Net.WebUtility.HtmlEncode(req.Notes ?? "")}</p>
    ";
        /*
         *         <p><b>Skip Size:</b> {req.SkipSize}</p>
                    <p><b>Material Type:</b> {req.MaterialType}</p>
        */

        var clientSubject = "Your Skip Booking Request - NTG Excavations";
        var clientHtml = $@"
        <p>Hi {req.FirstName},</p>
        <p>Thanks for your booking request. Here are your details:</p>
        {detailsHtml}
        <p>We’ll contact you shortly to confirm availability.</p>
        <p>— NTG Excavations</p>
    ";

        var adminSubject = $"NEW Skip Booking - {fullName} ({req.SkipSize})";
        var adminHtml = $@"<p><b>New booking received:</b></p>{detailsHtml}";

        await _emailSender.SendAsync(req.Email, clientSubject, clientHtml);
        await _emailSender.SendAsync(_emailSettings.AdminEmail, adminSubject, adminHtml);

        return Ok(new { message = "Booking submitted. Emails sent." });
    }

}
