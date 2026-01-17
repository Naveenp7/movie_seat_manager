using System;

namespace MovieBooking.Core.Entities;

public enum SeatStatus
{
    Available,
    Held,
    Booked
}

public class Seat
{
    public Guid Id { get; set; }
    public Guid ShowId { get; set; }
    public Show Show { get; set; } = null!;

    public string Row { get; set; } = string.Empty;
    public int Number { get; set; }

    public SeatStatus Status { get; set; } = SeatStatus.Available;
    
    /// <summary>
    /// If Held, when does the hold expire?
    /// </summary>
    public DateTime? HoldExpiryTime { get; set; }

    /// <summary>
    /// The UserID (or SessionID) holding or booking the seat.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Concurrency Token (Timestamp/RowVersion)
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
