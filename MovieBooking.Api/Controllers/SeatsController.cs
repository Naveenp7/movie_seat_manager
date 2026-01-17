using Microsoft.AspNetCore.Mvc;
using MovieBooking.Core.Interfaces;
using MovieBooking.Core.Entities;
using MovieBooking.Core.Entities;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Core.DTOs;

namespace MovieBooking.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SeatsController : ControllerBase
{
    private readonly ISeatService _seatService;
    private readonly MovieBooking.Infrastructure.Data.AppDbContext _context; // Direct DB access for 'list'
    private readonly ILogger<SeatsController> _logger;

    public SeatsController(ISeatService seatService, MovieBooking.Infrastructure.Data.AppDbContext context, ILogger<SeatsController> logger)
    {
        _seatService = seatService;
        _context = context;
        _logger = logger;
    }

    [HttpGet("shows")]
    public async Task<IActionResult> GetShows()
    {
        var shows = await _context.Shows.AsNoTracking().ToListAsync();
        return Ok(shows);
    }

    [HttpGet("{showId}/stats")]
    public async Task<IActionResult> GetStats(Guid showId)
    {
        var stats = await _context.Seats
            .Where(s => s.ShowId == showId)
            .GroupBy(s => 1)
            .Select(g => new ShowStatsDto
            {
                ShowId = showId,
                TotalSeats = g.Count(),
                AvailableSeats = g.Count(s => s.Status == SeatStatus.Available),
                HeldSeats = g.Count(s => s.Status == SeatStatus.Held),
                BookedSeats = g.Count(s => s.Status == SeatStatus.Booked)
            })
            .FirstOrDefaultAsync();

        return Ok(stats ?? new ShowStatsDto { ShowId = showId });
    }

    [HttpGet("{showId}")]
    public async Task<IActionResult> GetSeats(Guid showId)
    {
        var seats = await _seatService.GetSeatsAsync(showId);
        return Ok(seats);
    }

    [HttpPost("hold")]
    public async Task<IActionResult> HoldSeat([FromBody] HoldRequest request)
    {
        var success = await _seatService.HoldSeatAsync(request.SeatId, request.UserId);
        if (!success)
        {
            return Conflict("Seat is not available.");
        }
        return Ok("Seat held.");
    }

    [HttpPost("book")]
    public async Task<IActionResult> BookSeat([FromBody] BookRequest request)
    {
        var success = await _seatService.BookSeatAsync(request.SeatId, request.UserId);
        if (!success)
        {
            return BadRequest("Cannot book seat. It might have expired or be held by someone else.");
        }
        return Ok("Seat booked confirmed.");
    }
    [HttpPost("hold-bulk")]
    public async Task<IActionResult> HoldSeatsBulk([FromBody] BulkHoldRequest request)
    {
        var success = await _seatService.HoldSeatsAsync(request.SeatIds, request.UserId);
        if (!success)
        {
            return Conflict("One or more seats are not available.");
        }
        return Ok("Seats held.");
    }

    [HttpPost("release")]
    public async Task<IActionResult> ReleaseHold([FromBody] ReleaseRequest request)
    {
        var success = await _seatService.ReleaseHoldAsync(request.SeatId, request.UserId);
        if (!success) return BadRequest("Could not release seat.");
        return Ok("Seat released.");
    }

    [HttpPost("book-bulk")]
    public async Task<IActionResult> BookSeatsBulk([FromBody] BulkBookRequest request)
    {
        var success = await _seatService.BookSeatsAsync(request.SeatIds, request.UserId);
        if (!success) return BadRequest("Unable to book seats. They may have expired or not be held by you.");
        return Ok("Seats booked.");
    }

    [HttpPost("release-bulk")]
    public async Task<IActionResult> ReleaseSeatsBulk([FromBody] BulkReleaseRequest request)
    {
        var success = await _seatService.ReleaseSeatsAsync(request.SeatIds, request.UserId);
        if (!success) return BadRequest("Unable to release seats.");
        return Ok("Seats released.");
    }
}

public record HoldRequest(Guid SeatId, string UserId);
public record BulkHoldRequest(List<Guid> SeatIds, string UserId);
public record BookRequest(Guid SeatId, string UserId);
public record BulkBookRequest(List<Guid> SeatIds, string UserId);
public record ReleaseRequest(Guid SeatId, string UserId);
public record BulkReleaseRequest(List<Guid> SeatIds, string UserId);
