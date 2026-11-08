using System.Data;
using KargaBid.Api.Data;
using KargaBid.Api.Dtos;
using KargaBid.Api.Entities;
using KargaBid.Api.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KargaBid.Api.Controllers;

[ApiController]
[Route("api/bids")]
[Authorize]
public class BidsController : ControllerBase
{
    private readonly KargaBidDbContext _db;
    private readonly ILogger<BidsController> _logger;

    public BidsController(KargaBidDbContext db, ILogger<BidsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/bids/{id}/accept — Shipper accepts a bid.
    /// Atomically: marks the bid Accepted, rejects every other pending bid on the job,
    /// assigns the driver and moves the job to InProgress.
    /// </summary>
    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = Roles.Shipper)]
    [ProducesResponseType<AcceptBidResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AcceptBid(Guid id, CancellationToken ct)
    {
        var shipperId = User.GetUserId();

        await using var tx = await _db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        var bid = await _db.Bids
            .Include(b => b.Job)
            .SingleOrDefaultAsync(b => b.Id == id, ct);

        // 404 for bids on jobs this shipper does not own (don't leak existence).
        if (bid is null || bid.Job.ShipperId != shipperId)
            return NotFound();

        var job = bid.Job;

        if (job.Status != JobStatus.Open)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "A bid has already been accepted for this job.");

        if (bid.Status != BidStatus.Pending)
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: $"Only pending bids can be accepted (this bid is {bid.Status}).");

        // 1) Accept the bid and update the job. Job.RowVersion makes this fail with a
        //    DbUpdateConcurrencyException if another request changed the job after we read it.
        bid.Status = BidStatus.Accepted;
        job.Status = JobStatus.InProgress;
        job.AssignedDriverId = bid.DriverId;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            await tx.RollbackAsync(ct);
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "This job was updated by another request. Refresh and try again.");
        }
        catch (DbUpdateException ex) when (JobsController.IsUniqueViolation(ex))
        {
            // UX_Bids_JobId_Accepted: another accept won the race.
            await tx.RollbackAsync(ct);
            return Problem(statusCode: StatusCodes.Status409Conflict,
                title: "A bid has already been accepted for this job.");
        }

        // 2) Reject all other pending bids in one set-based UPDATE (same transaction).
        var rejected = await _db.Bids
            .Where(b => b.JobId == job.Id && b.Id != bid.Id && b.Status == BidStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, BidStatus.Rejected), ct);

        await tx.CommitAsync(ct);

        _logger.LogInformation(
            "Shipper {ShipperId} accepted bid {BidId} on job {JobId}; {Rejected} other bids rejected",
            shipperId, bid.Id, job.Id, rejected);

        return Ok(new AcceptBidResponse(bid.Id, job.Id, job.Status, bid.DriverId, rejected));
    }
}
