# KargaBid API

Backend for **KargaBid**, a reverse-auction logistics marketplace: shippers post cargo jobs, drivers bid down, the shipper accepts one bid.

**Stack:** ASP.NET Core 8 Web API · EF Core 8 (code-first) · SQL Server · JWT bearer auth

## Layout

```
src/KargaBid.Api/
  Entities/      User, Job, Bid, enums
  Data/          KargaBidDbContext (Fluent API mapping)
  Dtos/          request/response records
  Services/      JWT token issuing
  Controllers/   Auth, Jobs, Bids
database/schema.sql   hand-written T-SQL equivalent of the EF model (for review)
```

## Endpoints

| Method | Route | Role | Notes |
|---|---|---|---|
| POST | `/api/auth/register` | anonymous | returns JWT |
| POST | `/api/auth/login` | anonymous | returns JWT |
| POST | `/api/jobs` | Shipper | create job |
| GET | `/api/jobs/{id}` | owner / assigned driver / any driver while Open | |
| GET | `/api/jobs/open?page=&pageSize=` | Driver | newest first, paged |
| POST | `/api/jobs/{id}/bids` | Driver | one bid per driver per job; job must be Open |
| GET | `/api/jobs/{id}/bids` | Shipper (owner) | lowest bid first |
| POST | `/api/bids/{id}/accept` | Shipper (owner) | accepts bid, rejects the rest, job → InProgress, assigns driver — one transaction |
| PATCH | `/api/jobs/{id}/status` | Driver (assigned) | `InProgress → Completed` |

## Running locally

```bash
dotnet tool install --global dotnet-ef
cd src/KargaBid.Api
dotnet ef migrations add InitialCreate
dotnet ef database update
dotnet run
```

Swagger UI: `https://localhost:7180/swagger`.

`appsettings.Development.json` holds a dev-only JWT key and a LocalDB connection string. For any other environment, supply secrets via user-secrets or environment variables, e.g.:

```bash
dotnet user-secrets set "Jwt:SigningKey" "<at least 32 random bytes>"
dotnet user-secrets set "ConnectionStrings:KargaBid" "<connection string>"
```

The app refuses to start if `Jwt:SigningKey` is shorter than 32 characters.

## Design notes

- **Enums are stored as strings** (`NVARCHAR(20)`), money as `DECIMAL(18,2)`, timestamps as UTC `DATETIME2`.
- **Delete behavior:** `Job → Bids` cascades; all FKs to `Users` are `Restrict` (SQL Server rejects multiple cascade paths, and user history shouldn't vanish silently).
- **`Job.AssignedDriverId`** (not in the original spec) records the winning driver so only that driver can update delivery status.
- **Bid acceptance is race-safe:** `Job.RowVersion` (optimistic concurrency) plus a filtered unique index `UX_Bids_JobId_Accepted` guarantee at most one accepted bid per job; concurrent accepts get `409 Conflict`. Bid submission takes an `UPDLOCK` on the job row so no bid can land as `Pending` after the job is awarded.
- Resources owned by someone else return `404`, not `403`, so ids can't be probed.
