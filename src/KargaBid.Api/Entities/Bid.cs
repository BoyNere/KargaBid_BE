namespace KargaBid.Api.Entities;

public class Bid
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public Guid DriverId { get; set; }
    public decimal BidAmount { get; set; }
    public BidStatus Status { get; set; } = BidStatus.Pending;
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Job Job { get; set; } = null!;
    public User Driver { get; set; } = null!;
}
