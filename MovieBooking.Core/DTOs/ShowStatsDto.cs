using System;

namespace MovieBooking.Core.DTOs;

public class ShowStatsDto
{
    public Guid ShowId { get; set; }
    public int TotalSeats { get; set; }
    public int AvailableSeats { get; set; }
    public int HeldSeats { get; set; }
    public int BookedSeats { get; set; }
}
