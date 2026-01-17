using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MovieBooking.Core.Entities;
using MovieBooking.Core.Interfaces;
using MovieBooking.Infrastructure.Data;

namespace MovieBooking.Infrastructure.Services;

public class SeatService : ISeatService
{
    private readonly AppDbContext _context;

    public SeatService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Seat>> GetSeatsAsync(Guid showId)
    {
        return await _context.Seats
            .AsNoTracking() // Read-only optimization
            .Where(s => s.ShowId == showId)
            .OrderBy(s => s.Row).ThenBy(s => s.Number)
            .ToListAsync();
    }

    public async Task<bool> HoldSeatAsync(Guid seatId, string userId)
    {
        // 1. Start a transaction (optional but good for consistency if we did more)
        // EF Core SaveChanges is atomic, so strictly speaking transaction is implicit for single SaveChanges.
        
        try
        {
            var seat = await _context.Seats.FindAsync(seatId);
            if (seat == null) return false;

            // 2. Check Availability
            // It is available if: Status is Available OR (Status is Held AND Expired)
            bool isExpired = seat.Status == SeatStatus.Held && seat.HoldExpiryTime.HasValue && seat.HoldExpiryTime < DateTime.UtcNow;
            
            if (seat.Status == SeatStatus.Available || isExpired)
            {
                // 3. Update State
                seat.Status = SeatStatus.Held;
                seat.UserId = userId;
                seat.HoldExpiryTime = DateTime.UtcNow.AddMinutes(1); // 1 minute hold for testing
                // seat.RowVersion will be used for concurrency check

                // 4. Save
                await _context.SaveChangesAsync();
                return true;
            }

            return false; // Already taken or held map
        }
        catch (DbUpdateConcurrencyException)
        {
            // Concurrency Conflict: Someone updated the seat between our Read and Write.
            // In this scenario, it means the seat was taken.
            return false;
        }
    }

    public async Task<bool> BookSeatAsync(Guid seatId, string userId)
    {
        try
        {
            var seat = await _context.Seats.FindAsync(seatId);
            if (seat == null) return false;

            // 1. Validate Ownership
            if (seat.Status == SeatStatus.Held && seat.UserId == userId)
            {
                // Check expiry again just in case
                if (seat.HoldExpiryTime.HasValue && seat.HoldExpiryTime < DateTime.UtcNow)
                {
                    return false; // Expired while you were clicking
                }

                // 2. Update to Booked
                seat.Status = SeatStatus.Booked;
                seat.HoldExpiryTime = null; // Clear expiry
                
                // 3. Save
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
    }
}
