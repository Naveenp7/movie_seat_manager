using Microsoft.AspNetCore.Mvc;
using MovieBooking.Core.Interfaces;
using MovieBooking.Core.Entities;

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
}

public record HoldRequest(Guid SeatId, string UserId);
public record BookRequest(Guid SeatId, string UserId);
