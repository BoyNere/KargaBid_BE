using KargaBid.Api.Entities;
using Microsoft.EntityFrameworkCore;

namespace KargaBid.Api.Data;

public class KargaBidDbContext : DbContext
{
    public KargaBidDbContext(DbContextOptions<KargaBidDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<Bid> Bids => Set<Bid>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ---------- Users ----------
        modelBuilder.Entity<User>(e =>
        {
            e.ToTable("Users", t =>
            {
                t.HasCheckConstraint("CK_Users_Role", "[Role] IN (N'Shipper', N'Driver')");
            });
            e.HasKey(u => u.Id);
            e.Property(u => u.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            e.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            e.Property(u => u.Email).IsRequired().HasMaxLength(256);
            e.HasIndex(u => u.Email).IsUnique();

            e.Property(u => u.PasswordHash).IsRequired().HasMaxLength(512);

            e.Property(u => u.Role)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            e.Property(u => u.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");
        });

        // ---------- Jobs ----------
        modelBuilder.Entity<Job>(e =>
        {
            e.ToTable("Jobs", t =>
            {
                t.HasCheckConstraint("CK_Jobs_TargetBudget_Positive", "[TargetBudget] > 0");
                t.HasCheckConstraint("CK_Jobs_Status", "[Status] IN (N'Open', N'InProgress', N'Completed')");
            });
            e.HasKey(j => j.Id);
            e.Property(j => j.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            e.Property(j => j.PickupLocation).IsRequired().HasMaxLength(300);
            e.Property(j => j.DropOffLocation).IsRequired().HasMaxLength(300);
            e.Property(j => j.CargoDescription).IsRequired().HasMaxLength(2000);
            e.Property(j => j.TargetBudget).HasPrecision(18, 2);
            e.Property(j => j.CargoImageUrl).HasMaxLength(2048);

            e.Property(j => j.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(JobStatus.Open)
                .HasSentinel((JobStatus)(-1)) // so EF still sends Open explicitly
                .IsRequired();

            e.Property(j => j.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            e.Property(j => j.RowVersion).IsRowVersion();

            // Shipper (1) -> Jobs (many). Restrict: never silently wipe a shipper's job history.
            e.HasOne(j => j.Shipper)
                .WithMany(u => u.PostedJobs)
                .HasForeignKey(j => j.ShipperId)
                .OnDelete(DeleteBehavior.Restrict);

            // Assigned driver (optional). Restrict avoids multiple cascade paths in SQL Server.
            e.HasOne(j => j.AssignedDriver)
                .WithMany(u => u.AssignedJobs)
                .HasForeignKey(j => j.AssignedDriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // "GET /api/jobs/open" filters by Status and sorts by CreatedAt.
            e.HasIndex(j => new { j.Status, j.CreatedAt });
            e.HasIndex(j => j.ShipperId);
            e.HasIndex(j => j.AssignedDriverId);
        });

        // ---------- Bids ----------
        modelBuilder.Entity<Bid>(e =>
        {
            e.ToTable("Bids", t =>
            {
                t.HasCheckConstraint("CK_Bids_BidAmount_Positive", "[BidAmount] > 0");
                t.HasCheckConstraint("CK_Bids_Status", "[Status] IN (N'Pending', N'Accepted', N'Rejected')");
            });
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).HasDefaultValueSql("NEWSEQUENTIALID()");

            e.Property(b => b.BidAmount).HasPrecision(18, 2);

            e.Property(b => b.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .HasDefaultValue(BidStatus.Pending)
                .HasSentinel((BidStatus)(-1))
                .IsRequired();

            e.Property(b => b.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()");

            // Job (1) -> Bids (many). Deleting a job removes its bids.
            e.HasOne(b => b.Job)
                .WithMany(j => j.Bids)
                .HasForeignKey(b => b.JobId)
                .OnDelete(DeleteBehavior.Cascade);

            // Driver (1) -> Bids (many). Restrict (Users -> Jobs -> Bids would be a 2nd cascade path).
            e.HasOne(b => b.Driver)
                .WithMany(u => u.Bids)
                .HasForeignKey(b => b.DriverId)
                .OnDelete(DeleteBehavior.Restrict);

            // One bid per driver per job.
            e.HasIndex(b => new { b.JobId, b.DriverId }).IsUnique();

            // DB-level guarantee: at most one Accepted bid per job.
            e.HasIndex(b => b.JobId)
                .IsUnique()
                .HasFilter("[Status] = N'Accepted'")
                .HasDatabaseName("UX_Bids_JobId_Accepted");

            e.HasIndex(b => b.DriverId);
        });
    }
}
