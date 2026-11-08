using KargaBid.Api.Data;
using KargaBid.Api.Dtos;
using KargaBid.Api.Entities;
using KargaBid.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace KargaBid.Api.Controllers;

[ApiController]
[Route("api/jobs")]
[Authorize] // every endpoint requires a valid JWT; roles narrowed per action
public class JobsController : ControllerBase
{
    private readonly KargaBidDbContext _db;

    public JobsController(KargaBidDbContext db) => _db = db;

    // POST /api/jobs  — Shipper creates a job
    [HttpPost]
    [Authorize(Roles = Roles.Shipper)]
    [ProducesResponseType<JobResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateJob(CreateJobRequest request, CancellationToken ct)
    {
        var job = new Job
        {
            ShipperId = User.GetUserId(),
            PickupLocation = request.PickupLocation.Trim(),
            DropOffLocation = request.DropOffLocation.Trim(),
            CargoDescription = request.CargoDescription.Trim(),
            TargetBudget = request.TargetBudget,
            CargoImageUrl = request.CargoImageUrl,
            Status = JobStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        _db.Jobs.Add(job);
        await _db.SaveChangesAsync(ct);

        var response = await ProjectJobs(_db.Jobs.Where(j => j.Id == job.Id)).SingleAsync(ct);
        return CreatedAtAction(nameof(GetJob), new { id = job.Id }, response);
    }

    // GET /api/jobs/{id} — job owner, assigned driver, or any driver while the job is open
    [HttpGet("{id:guid}")]
    [ProducesResponseType<JobResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJob(Guid id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        var isDriver = User.IsInRole(Roles.Driver);

        var job = await ProjectJobs(_db.Jobs.Where(j =>
                j.Id == id &&
                (j.ShipperId == userId ||
                 j.AssignedDriverId == userId ||
                 (isDriver && j.Status == JobStatus.Open))))
            .SingleOrDefaultAsync(ct);

        return job is null ? NotFound() : Ok(job);
    }

    // GET /api/jobs/open — Driver fetches available jobs
    [HttpGet("open")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<IReadOnlyList<JobResponse>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOpenJobs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var jobs = await ProjectJobs(_db.Jobs
                .Where(j => j.Status == JobStatus.Open)
                .OrderByDescending(j => j.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(ct);

        return Ok(jobs);
    }

    // POST /api/jobs/{id}/bids — Driver submits a bid
    [HttpPost("{id:guid}/bids")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<BidResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitBid(Guid id, CreateBidRequest request, CancellationToken ct)
    {
        var driverId = User.GetUserId();

        // Hold an update lock on the job row for the duration of the insert. This serializes
        // against POST /api/bids/{id}/accept, so a bid can never slip in as "Pending" after the
        // job has been awarded (accept either sees this bid and rejects it, or we see InProgress).
        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var job = await _db.Jobs
            .FromSql($"SELECT * FROM [Jobs] WITH (UPDLOCK, ROWLOCK) WHERE [Id] = {id}")
            .AsNoTracking()
            .Select(j => new { j.Id, j.Status, j.ShipperId })
            .SingleOrDefaultAsync(ct);

        if (job is null)
            return NotFound();

        if (job.Status != JobStatus.Open)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "This job is no longer accepting bids.");

        if (job.ShipperId == driverId) // defensive; roles should already prevent this
            return Problem(statusCode: StatusCodes.Status403Forbidden,
                title: "You cannot bid on your own job.");

        if (await _db.Bids.AnyAsync(b => b.JobId == id && b.DriverId == driverId, ct))
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "You have already placed a bid on this job.");

        var bid = new Bid
        {
            JobId = id,
            DriverId = driverId,
            BidAmount = request.BidAmount,
            Status = BidStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.Bids.Add(bid);
        try
        {
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Double-submit race against UX on (JobId, DriverId).
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "You have already placed a bid on this job.");
        }

        var response = await _db.Bids
            .Where(b => b.Id == bid.Id)
            .Select(b => new BidResponse(b.Id, b.JobId, b.DriverId, b.Driver.FullName,
                b.BidAmount, b.Status, b.CreatedAt))
            .SingleAsync(ct);

        return CreatedAtAction(nameof(GetBidsForJob), new { id }, response);
    }

    // GET /api/jobs/{id}/bids — Shipper views bids on their own job (lowest first)
    [HttpGet("{id:guid}/bids")]
    [Authorize(Roles = Roles.Shipper)]
    [ProducesResponseType<IReadOnlyList<BidResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetBidsForJob(Guid id, CancellationToken ct)
    {
        var shipperId = User.GetUserId();

        // 404 (not 403) for jobs owned by someone else, so job ids can't be probed.
        var ownsJob = await _db.Jobs.AnyAsync(j => j.Id == id && j.ShipperId == shipperId, ct);
        if (!ownsJob)
            return NotFound();

        var bids = await _db.Bids
            .AsNoTracking()
            .Where(b => b.JobId == id)
            .OrderBy(b => b.BidAmount)
            .ThenBy(b => b.CreatedAt)
            .Select(b => new BidResponse(b.Id, b.JobId, b.DriverId, b.Driver.FullName,
                b.BidAmount, b.Status, b.CreatedAt))
            .ToListAsync(ct);

        return Ok(bids);
    }

    // PATCH /api/jobs/{id}/status — assigned Driver updates delivery status
    [HttpPatch("{id:guid}/status")]
    [Authorize(Roles = Roles.Driver)]
    [ProducesResponseType<JobResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateJobStatusRequest request, CancellationToken ct)
    {
        var driverId = User.GetUserId();

        var job = await _db.Jobs.SingleOrDefaultAsync(j => j.Id == id && j.AssignedDriverId == driverId, ct);
        if (job is null)
            return NotFound();

        // Allowed driver transitions: InProgress -> Completed (idempotent if already there).
        var allowed = (job.Status, request.Status) switch
        {
            (JobStatus.InProgress, JobStatus.Completed) => true,
            var (from, to) when from == to && from != JobStatus.Open => true,
            _ => false
        };

        if (!allowed)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Cannot change job status from {job.Status} to {request.Status}.");

        if (job.Status != request.Status)
        {
            job.Status = request.Status;
            try
            {
                await _db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Problem(statusCode: StatusCodes.Status409Conflict,
                    title: "The job was modified by another request. Please retry.");
            }
        }

        var response = await ProjectJobs(_db.Jobs.Where(j => j.Id == id)).SingleAsync(ct);
        return Ok(response);
    }

    private static IQueryable<JobResponse> ProjectJobs(IQueryable<Job> query) =>
        query.AsNoTracking().Select(j => new JobResponse(
            j.Id,
            j.ShipperId,
            j.Shipper.FullName,
            j.PickupLocation,
            j.DropOffLocation,
            j.CargoDescription,
            j.TargetBudget,
            j.Status,
            j.CargoImageUrl,
            j.AssignedDriverId,
            j.Bids.Count,
            j.Bids.Min(b => (decimal?)b.BidAmount),
            j.CreatedAt));

    internal static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
