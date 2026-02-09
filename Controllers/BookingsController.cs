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

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] BookingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.FirstName))
            return BadRequest(new { message = "Missing required fields." });

        if (!DateOnly.TryParse(req.PreferredDate, out var dateOnly))
            return BadRequest(new { message = "PreferredDate must be YYYY-MM-DD." });

        var allowedSlots = new[] { "08:00", "09:00", "10:00", "11:00", "12:00" };
        if (!allowedSlots.Contains(req.TimeSlot))
            return BadRequest(new { message = "Invalid time slot." });

        var today = DateOnly.FromDateTime(DateTime.Now);

        if (dateOnly < today)
            return BadRequest(new { message = "PreferredDate cannot be in the past." });

        if (dateOnly.DayOfWeek == DayOfWeek.Sunday)
            return BadRequest(new { message = "Bookings are not available on Sundays." });

        if (dateOnly == today && DateTime.Now.TimeOfDay >= new TimeSpan(12, 0, 0))
            return BadRequest(new { message = "Same-day bookings are not available after 12:00." });

        var booking = new Booking
        {
            FirstName = req.FirstName.Trim(),
            LastName = (req.LastName ?? "").Trim(),
            Email = req.Email.Trim(),
            Mobile = (req.Mobile ?? "").Trim(),
            Address = (req.Address ?? "").Trim(),
            PreferredDate = dateOnly,
            TimeSlot = req.TimeSlot,
            Notes = req.Notes,
            Status = "Pending",
            CreatedUtc = DateTime.UtcNow
        };

        _db.Bookings.Add(booking);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict(new { message = "That time slot is already booked for this date." });
        }


        // Build email HTML
        var fullName = $"{booking.FirstName} {booking.LastName}".Trim();
        var detailsHtml = $@"
        <h2>Skip Booking Details</h2>
        <p><b>Name:</b> {fullName}</p>
        <p><b>Email:</b> {booking.Email}</p>
        <p><b>Mobile:</b> {booking.Mobile}</p>
        <p><b>Address:</b> {booking.Address}</p>
        <p><b>Preferred Date:</b> {dateOnly:dddd, dd MMM yyyy}</p>
        <p><b>Preferred Time:</b> {booking.TimeSlot}</p>
        <p><b>Additional Info:</b> {System.Net.WebUtility.HtmlEncode(booking.Notes ?? "")}</p>
    ";

        var clientSubject = "Your Booking Request - NTG Excavations";
        var clientHtml = $@"
        <p>Hi {booking.FirstName},</p>
        <p>Thanks for your booking request. Here are your details:</p>
        {detailsHtml}
        <p>We’ll contact you shortly to confirm availability.</p>
        <p>— NTG Excavations</p>
    ";

        var adminSubject = $"NEW Booking - {fullName}";
        var adminHtml = $@"<p><b>New booking received:</b></p>{detailsHtml}";
        var emailFailed = false;

        try
        {
            // Run email in background-ish with a hard timeout
            var t1 = _emailSender.SendAsync(booking.Email, clientSubject, clientHtml);
            var t2 = _emailSender.SendAsync(_emailSettings.AdminEmail, adminSubject, adminHtml);

            var all = Task.WhenAll(t1, t2);
            var finished = await Task.WhenAny(all, Task.Delay(8000));

            if (finished != all)
                emailFailed = true;
        }
        catch
        {
            emailFailed = true;
        }

        // ALWAYS OK if booking saved
        return Ok(new
        {
            message = emailFailed
                ? "Booking saved, but email delivery is temporarily unavailable."
                : "Booking submitted. Emails sent.",
            bookingId = booking.Id
        });

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

    [HttpPut("admin/{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(int id, [FromBody] UpdateBookingStatusRequest req)
    {
        if (!IsAdmin()) return Unauthorized();

        var allowed = new[] { "Confirmed", "Rejected" };
        if (!allowed.Contains(req.Status))
            return BadRequest(new { message = "Invalid status." });

        var booking = await _db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        // ❌ REJECT → DELETE
        if (req.Status == "Rejected")
        {
            _db.Bookings.Remove(booking);
            await _db.SaveChangesAsync();

            return Ok(new { deleted = true, bookingId = id });
        }

        // ✅ CONFIRM (send email only if changing to confirmed)
        var wasConfirmed = booking.Status == "Confirmed";
        booking.Status = "Confirmed";
        await _db.SaveChangesAsync();

        var emailSent = false;
        string? emailError = null;

        if (!wasConfirmed)
        {
            try
            {
                var fullName = $"{booking.FirstName} {booking.LastName}".Trim();
                var subject = "Booking Confirmed - NTG Excavations";

                var html = $@"
                <p>Hi {booking.FirstName},</p>
                <p><b>Your booking has been confirmed ✅</b></p>

                <h3>Booking Details</h3>
                <p><b>Name:</b> {fullName}</p>
                <p><b>Date:</b> {booking.PreferredDate:dddd, dd MMM yyyy}</p>
                <p><b>Time:</b> {booking.TimeSlot}</p>
                <p><b>Address:</b> {System.Net.WebUtility.HtmlEncode(booking.Address)}</p>

                <p>— NTG Excavations</p>
            ";

                // add a timeout so your API doesn't hang if email provider is slow
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await _emailSender.SendAsync(booking.Email, subject, html, cts.Token);

                emailSent = true;
            }
            catch (Exception ex)
            {
                emailError = ex.Message;
                Console.WriteLine("Confirmation email failed:");
                Console.WriteLine(ex);
            }
        }

        return Ok(new
        {
            booking.Id,
            booking.Status,
            emailSent,
            emailError
        });
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

    [HttpDelete("admin/{id:int}")]
    public async Task<IActionResult> DeleteBooking(int id, [FromServices] AppDbContext db)
    {
        if (!IsAdmin()) return Unauthorized();

        var booking = await db.Bookings.FindAsync(id);
        if (booking == null) return NotFound();

        db.Bookings.Remove(booking);
        await db.SaveChangesAsync();

        return Ok(new { message = "Booking deleted." });
    }

    // GET api/bookings/admin/stats
    [HttpGet("admin/stats")]
    public async Task<IActionResult> GetAdminStats()
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


}
