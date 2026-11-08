using System.ComponentModel.DataAnnotations;
using KargaBid.Api.Entities;

namespace KargaBid.Api.Dtos;

public record CreateJobRequest(
    [Required, StringLength(300)] string PickupLocation,
    [Required, StringLength(300)] string DropOffLocation,
    [Required, StringLength(2000)] string CargoDescription,
    [Range(typeof(decimal), "0.01", "9999999999999999.99")] decimal TargetBudget,
    [Url, StringLength(2048)] string? CargoImageUrl);

public record UpdateJobStatusRequest(
    [Required, EnumDataType(typeof(JobStatus))] JobStatus Status);

public record JobResponse(
    Guid Id,
    Guid ShipperId,
    string ShipperName,
    string PickupLocation,
    string DropOffLocation,
    string CargoDescription,
    decimal TargetBudget,
    JobStatus Status,
    string? CargoImageUrl,
    Guid? AssignedDriverId,
    int BidCount,
    decimal? LowestBid,
    DateTime CreatedAt);
