namespace KargaBid.Api.Entities;

public class Job
{
    public Guid Id { get; set; }
    public Guid ShipperId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropOffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public decimal TargetBudget { get; set; }
    public JobStatus Status { get; set; } = JobStatus.Open;
    public string? CargoImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Driver whose bid was accepted. Null while the job is Open.
    /// Needed so only the winning driver can update delivery status.
    /// </summary>
    public Guid? AssignedDriverId { get; set; }

    /// <summary>Optimistic concurrency token (SQL Server rowversion).</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    // Navigation
    public User Shipper { get; set; } = null!;
    public User? AssignedDriver { get; set; }
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();
}
