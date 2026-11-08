namespace KargaBid.Api.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public ICollection<Job> PostedJobs { get; set; } = new List<Job>();   // as Shipper
    public ICollection<Job> AssignedJobs { get; set; } = new List<Job>(); // as Driver
    public ICollection<Bid> Bids { get; set; } = new List<Bid>();         // as Driver
}
