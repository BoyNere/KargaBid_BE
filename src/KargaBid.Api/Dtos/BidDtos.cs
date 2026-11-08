using System.ComponentModel.DataAnnotations;
using KargaBid.Api.Entities;

namespace KargaBid.Api.Dtos;

public record CreateBidRequest(
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal BidAmount);

public record BidResponse(
    Guid Id,
    Guid JobId,
    Guid DriverId,
    string DriverName,
    decimal BidAmount,
    BidStatus Status,
    DateTime CreatedAt);

public record AcceptBidResponse(
    Guid AcceptedBidId,
    Guid JobId,
    JobStatus JobStatus,
    Guid AssignedDriverId,
    int RejectedBidCount);
