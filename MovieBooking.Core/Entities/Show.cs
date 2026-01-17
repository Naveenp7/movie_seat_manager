using System;
using System.Collections.Generic;

namespace MovieBooking.Core.Entities;

public class Show
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }

    public ICollection<Seat> Seats { get; set; } = new List<Seat>();
}
