namespace KargaBid.Api.Entities;

public enum UserRole
{
    Shipper,
    Driver
}

public enum JobStatus
{
    Open,
    InProgress,
    Completed
}

public enum BidStatus
{
    Pending,
    Accepted,
    Rejected
}
