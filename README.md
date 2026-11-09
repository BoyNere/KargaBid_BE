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
database/
  schema.sql        T-SQL tables (re-runnable; mirrors the EF model)
  seed.sql          dummy data for local testing
  setup-local.ps1   one-shot LocalDB setup (create DB + schema + seed)
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

## Running locally (LocalDB)

**1. Create the database, tables and dummy data** (Windows, from the repo root):

```powershell
.\database\setup-local.ps1            # add -Recreate to drop and rebuild the DB
```

This creates `KargaBid` on `(localdb)\MSSQLLocalDB`, runs `database/schema.sql`, then `database/seed.sql`. If you prefer raw `sqlcmd` (or SSMS: create the DB, then run the two files against it):

```bash
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -Q "CREATE DATABASE KargaBid"
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -I -d KargaBid -i database/schema.sql
sqlcmd -S "(localdb)\MSSQLLocalDB" -E -I -d KargaBid -i database/seed.sql
```

> Use `-I`: the filtered index on `Bids` needs `QUOTED_IDENTIFIER ON`, and sqlcmd defaults to OFF.

**2. Run the API:**

```bash
cd src/KargaBid.Api
dotnet run
```

Swagger UI: `https://localhost:7180/swagger`. Ready-made requests for every endpoint are in `src/KargaBid.Api/KargaBid.Api.http`.

### Seeded test data

All passwords are `Password123!`.

| User | Email | Role | Id |
|---|---|---|---|
| Maria Santos | maria.santos@kargabid.test | Shipper | `11111111-0000-0000-0000-000000000001` |
| Jose Reyes | jose.reyes@kargabid.test | Shipper | `11111111-0000-0000-0000-000000000002` |
| Juan Dela Cruz | juan.delacruz@kargabid.test | Driver | `11111111-0000-0000-0000-000000000003` |
| Pedro Garcia | pedro.garcia@kargabid.test | Driver | `11111111-0000-0000-0000-000000000004` |
| Ana Lopez | ana.lopez@kargabid.test | Driver | `11111111-0000-0000-0000-000000000005` |

| Job | Shipper | Status | Bids |
|---|---|---|---|
| J1 `22222222-…-000000000001` QC → Batangas | Maria | Open | Juan 8,500 · Pedro 7,900 (pending) |
| J2 `22222222-…-000000000002` Pasig → Clark | Maria | Open | none |
| J3 `22222222-…-000000000003` Makati → Tagaytay | Jose | Open | Ana 4,200 (pending) |
| J4 `22222222-…-000000000004` Valenzuela → Baguio | Maria | InProgress (Pedro) | Pedro accepted · Juan, Ana rejected |
| J5 `22222222-…-000000000005` Taguig → Lipa | Jose | Completed (Juan) | Juan accepted · Pedro rejected |
| J6 `22222222-…-000000000006` Caloocan → San Fernando | Jose | Open | Juan 7,600 · Pedro 7,200 · Ana 7,450 (pending) |

Bid ids are `33333333-0000-0000-0000-0000000000NN` (01–11, in the order above). Re-run `seed.sql` to reset the data after testing.

### Database: scripts vs. EF migrations

The SQL scripts and EF migrations both create the same tables, so use one or the other per database: an `InitialCreate` migration will fail against tables created by `schema.sql`. To switch, run migrations against an empty database. `seed.sql` works on top of either.

For any non-local environment, supply secrets via user-secrets or environment variables:

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
