namespace KutuphaneYonetim.Domain.Entities;

/// <summary>State of a reservation. The numeric values are stored in the database.</summary>
public enum ReservationStatus
{
    /// <summary>In the queue while the book is on loan (or held for someone earlier in the queue).</summary>
    Waiting = 0,

    /// <summary>The book was returned and is held for this member until <see cref="Reservation.ExpiresAt"/>.</summary>
    Ready = 1,

    /// <summary>The member borrowed the book.</summary>
    Fulfilled = 2,

    Cancelled = 3,

    /// <summary>The member did not pick up the held book in time.</summary>
    Expired = 4,
}

/// <summary>A member's place in the queue for a book (table: Rezervasyonlar).</summary>
public sealed class Reservation
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public int MemberId { get; set; }

    /// <summary>When the reservation was made; the queue is served in this order (then by id).</summary>
    public DateTime CreatedAt { get; set; }

    public ReservationStatus Status { get; set; }

    /// <summary>When the book started to be held for the member.</summary>
    public DateTime? ReadyAt { get; set; }

    /// <summary>The hold ends after this moment; the reservation then expires and the next member is served.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>When the reservation was fulfilled, cancelled or expired.</summary>
    public DateTime? ClosedAt { get; set; }

    public bool IsActive => Status is ReservationStatus.Waiting or ReservationStatus.Ready;
}

/// <summary>Read model for listing reservations with book and member names.</summary>
public sealed class ReservationDetail
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public string BookTitle { get; set; } = string.Empty;

    public int MemberId { get; set; }

    public string MemberName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public ReservationStatus Status { get; set; }

    public DateTime? ReadyAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    /// <summary>0 for the member the book is held for, 1 for the first waiting member, and so on.</summary>
    public int QueuePosition { get; set; }
}
