using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MovieBooking.Core.Entities;

namespace MovieBooking.Core.Interfaces;

public interface ISeatService
{
    Task<List<Seat>> GetSeatsAsync(Guid showId);
    Task<bool> HoldSeatAsync(Guid seatId, string userId);
    Task<bool> BookSeatAsync(Guid seatId, string userId);
}
