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
                
                // SignalR Broadcast
                await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seatId, (int)SeatStatus.Held, userId);
                await BroadcastStats(seat.ShowId);
                
                return true;
            }
            return false;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    public async Task<bool> HoldSeatsAsync(List<Guid> seatIds, string userId)
    {
        // Atomic Bulk Hold
        if (seatIds == null || !seatIds.Any()) return false;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // Sort to prevent deadlocks
            var sortedIds = seatIds.OrderBy(id => id).ToList();
            
            var seats = await _context.Seats
                .Where(s => sortedIds.Contains(s.Id))
                .ToListAsync();

            if (seats.Count != sortedIds.Count) return false; // Some seats not found

            foreach (var seat in seats)
            {
                bool isExpired = seat.Status == SeatStatus.Held && seat.HoldExpiryTime < DateTime.UtcNow;
                bool isMine = seat.Status == SeatStatus.Held && seat.UserId == userId;

                // 1. If it's validly held by someone else, Fail.
                if (seat.Status != SeatStatus.Available && !isExpired && !isMine)
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // 2. If it's Booked, Fail.
                if (seat.Status == SeatStatus.Booked) 
                {
                    await transaction.RollbackAsync();
                    return false;
                }

                // 3. Otherwise (Available OR Expired OR Mine), Take it.
                seat.Status = SeatStatus.Held;
                seat.UserId = userId;
                seat.HoldExpiryTime = DateTime.UtcNow.AddMinutes(1);
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Broadcast Updates
            foreach (var seat in seats)
            {
                await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seat.Id, (int)SeatStatus.Held, userId);
            }
            if(seats.Any()) await BroadcastStats(seats.First().ShowId);

            return true;
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task<bool> ReleaseHoldAsync(Guid seatId, string userId)
    {
        var seat = await _context.Seats.FindAsync(seatId);
        if (seat == null) return false;

        // Only allow user who held it to release it
        if (seat.Status == SeatStatus.Held && seat.UserId == userId)
        {
            seat.Status = SeatStatus.Available;
            seat.UserId = null;
            seat.HoldExpiryTime = null;
            
            await _context.SaveChangesAsync();
            
            await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seatId, (int)SeatStatus.Available, null);
            await BroadcastStats(seat.ShowId);
            return true;
        }
        return false;
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
                
                await _hubContext.Clients.All.SendAsync("ReceiveSeatUpdate", seatId, (int)SeatStatus.Booked, userId);
                await BroadcastStats(seat.ShowId);
                return true;
            }
            return false;
        }
        catch (DbUpdateConcurrencyException) { return false; }
    }

    private async Task BroadcastStats(Guid showId)
    {
        // Ideally, calculate stats and push. For now, just trigger clients to fetch.
        // Or we can send the actual numbers. Let's send a trigger signal to keep it simple.
        await _hubContext.Clients.All.SendAsync("RefreshStats", showId);
    }
}
