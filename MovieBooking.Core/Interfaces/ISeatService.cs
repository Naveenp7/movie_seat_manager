using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MovieBooking.Core.Entities;

namespace MovieBooking.Core.Interfaces;

public interface ISeatService
{
    Task<List<Seat>> GetSeatsAsync(Guid showId);
    Task<bool> HoldSeatAsync(Guid seatId, string userId);
    Task<bool> HoldSeatsAsync(List<Guid> seatIds, string userId); // New: Bulk
    Task<bool> BookSeatAsync(Guid seatId, string userId);
    Task<bool> ReleaseHoldAsync(Guid seatId, string userId); // New: Cancel
}
