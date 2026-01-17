using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using MovieBooking.Core.Entities;
using MovieBooking.Core.Interfaces;
using MovieBooking.Infrastructure.Data;
using MovieBooking.Infrastructure.Hubs;

namespace MovieBooking.Infrastructure.Services;

public class SeatService : ISeatService
{
    private readonly AppDbContext _context;
    private readonly IHubContext<SeatHub> _hubContext;

    public SeatService(AppDbContext context, IHubContext<SeatHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    public async Task<List<Seat>> GetSeatsAsync(Guid showId)
    {
        return await _context.Seats
            .AsNoTracking()
            .Where(s => s.ShowId == showId)
            .OrderBy(s => s.Row).ThenBy(s => s.Number)
            .ToListAsync();
    }

    public async Task<bool> HoldSeatAsync(Guid seatId, string userId)
    {
        // ... Single Hold implementation (omitted for brevity if purely adding new methods, but here we replace whole file so keep it included or use multi-replace.
        // Wait, replace_file_content replaces the WHOLE file. I should use multi_replace if I only want to append. 
        // But the viewing showed the whole file was small enough. 
        // Let's Stick to the existing logic and append the new methods.
        // Actually, re-writing the whole file ensures cleanliness given the previous edits.
        
        // Single Hold Logic
        try
        {
            var seat = await _context.Seats.FindAsync(seatId);
            if (seat == null) return false;

            bool isExpired = seat.Status == SeatStatus.Held && seat.HoldExpiryTime.HasValue && seat.HoldExpiryTime < DateTime.UtcNow;
            
            if (seat.Status == SeatStatus.Available || isExpired)
            {
                seat.Status = SeatStatus.Held;
                seat.UserId = userId;
                seat.HoldExpiryTime = DateTime.UtcNow.AddMinutes(1);
                
                await _context.SaveChangesAsync();
                await BroadcastUpdate(seatId, SeatStatus.Held, userId, seat.ShowId);
                return true;
            }
            return false;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<bool> HoldSeatsAsync(List<Guid> seatIds, string userId)
    {
        if (seatIds == null || !seatIds.Any()) return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var sortedIds = seatIds.OrderBy(id => id).ToList();
            var seats = await _context.Seats.Where(s => sortedIds.Contains(s.Id)).ToListAsync();

            if (seats.Count != sortedIds.Count) return false;

            foreach (var seat in seats)
            {
                bool isExpired = seat.Status == SeatStatus.Held && seat.HoldExpiryTime < DateTime.UtcNow;
                bool isMine = seat.Status == SeatStatus.Held && seat.UserId == userId;

                if (seat.Status != SeatStatus.Available && !isExpired && !isMine)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                if (seat.Status == SeatStatus.Booked)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                seat.Status = SeatStatus.Held;
                seat.UserId = userId;
                seat.HoldExpiryTime = DateTime.UtcNow.AddMinutes(1);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _ = Task.Run(async () => {
                 foreach(var s in seats) await BroadcastUpdate(s.Id, SeatStatus.Held, userId, s.ShowId);
            });
            
            return true;
        }
        catch (Exception)
        {
            try { await transaction.RollbackAsync(); } catch {}
            return false;
        }
    }

    public async Task<bool> ReleaseHoldAsync(Guid seatId, string userId)
    {
        var seat = await _context.Seats.FindAsync(seatId);
        if (seat == null) return false;

        if (seat.Status == SeatStatus.Held && seat.UserId == userId)
        {
            seat.Status = SeatStatus.Available;
            seat.UserId = null;
            seat.HoldExpiryTime = null;
            
            await _context.SaveChangesAsync();
            await BroadcastUpdate(seatId, SeatStatus.Available, null, seat.ShowId);
            return true;
        }
        return false;
    }

    public async Task<bool> ReleaseSeatsAsync(List<Guid> seatIds, string userId)
    {
         if (seatIds == null || !seatIds.Any()) return false;
         using var transaction = await _context.Database.BeginTransactionAsync();
         try
         {
             var seats = await _context.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();
             foreach(var seat in seats)
             {
                 if(seat.Status == SeatStatus.Held && seat.UserId == userId)
                 {
                     seat.Status = SeatStatus.Available;
                     seat.UserId = null;
                     seat.HoldExpiryTime = null;
                 }
                 // If not held by me, ignore or fail? For release, ignoring is safer/fine, specifically "release mine".
             }
             await _context.SaveChangesAsync();
             await transaction.CommitAsync();
             
             _ = Task.Run(async () => {
                 foreach(var s in seats) await BroadcastUpdate(s.Id, SeatStatus.Available, null, s.ShowId);
             });
             return true;
         }
         catch
         {
             try { await transaction.RollbackAsync(); } catch {}
             return false;
         }
    }

    public async Task<bool> BookSeatAsync(Guid seatId, string userId)
    {
        try
        {
            var seat = await _context.Seats.FindAsync(seatId);
            if (seat == null) return false;
            
            if (seat.Status == SeatStatus.Booked && seat.UserId == userId) return true;

            if (seat.Status == SeatStatus.Held && seat.UserId == userId)
            {
                if (seat.HoldExpiryTime < DateTime.UtcNow) return false;

                seat.Status = SeatStatus.Booked;
                seat.HoldExpiryTime = null;
                
                await _context.SaveChangesAsync();
                await BroadcastUpdate(seatId, SeatStatus.Booked, userId, seat.ShowId);
                return true;
            }
            return false;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<bool> BookSeatsAsync(List<Guid> seatIds, string userId)
    {
        if (seatIds == null || !seatIds.Any()) return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var seats = await _context.Seats.Where(s => seatIds.Contains(s.Id)).ToListAsync();
            // Validate ALL are held by user
            foreach (var seat in seats)
            {
                if (seat.Status == SeatStatus.Booked && seat.UserId == userId) continue; // Already booked, fine.

                if (seat.Status != SeatStatus.Held || seat.UserId != userId)
                {
                    await transaction.RollbackAsync();
                    return false; 
                }
                if (seat.HoldExpiryTime < DateTime.UtcNow)
                {
                     await transaction.RollbackAsync();
                     return false; // Expired
                }

                seat.Status = SeatStatus.Booked;
                seat.HoldExpiryTime = null;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _ = Task.Run(async () => {
                 foreach(var s in seats) await BroadcastUpdate(s.Id, SeatStatus.Booked, userId, s.ShowId);
            });

            return true;
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch {}
            return false;
        }
    }

    private async Task BroadcastUpdate(Guid seatId, SeatStatus status, string? userId, Guid showId)
    {
        try {
            await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seatId, (int)status, userId);
            await _hubContext.Clients.All.SendAsync("RefreshStats", showId);
        } catch {}
    }
}
