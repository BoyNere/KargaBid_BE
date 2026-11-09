/* ============================================================================
   KargaBid - dummy data for local endpoint testing
   Run after schema.sql (or after `dotnet ef database update`):
     sqlcmd -S "(localdb)\MSSQLLocalDB" -d KargaBid -I -i database\seed.sql

   Re-runnable: wipes all rows first.

   Every user's password is:  Password123!
   (Hashes are ASP.NET Core Identity v3 format: PBKDF2-HMACSHA512, 100k iterations,
    so POST /api/auth/login works for these accounts.)

   Fixed ids so you can paste them into requests:
     Users  11111111-...-0000000000XX
     Jobs   22222222-...-0000000000XX
     Bids   33333333-...-0000000000XX
   ============================================================================ */
SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;  -- required: Bids has a filtered unique index
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;

DELETE FROM [dbo].[Bids];
DELETE FROM [dbo].[Jobs];
DELETE FROM [dbo].[Users];

DECLARE @now DATETIME2 = SYSUTCDATETIME();

/* ---------- Users ---------- */
-- Shippers
DECLARE @maria UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000001';
DECLARE @jose  UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000002';
-- Drivers
DECLARE @juan  UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000003';
DECLARE @pedro UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000004';
DECLARE @ana   UNIQUEIDENTIFIER = '11111111-0000-0000-0000-000000000005';

INSERT INTO [dbo].[Users] ([Id], [FullName], [Email], [PasswordHash], [Role], [CreatedAt]) VALUES
(@maria, N'Maria Santos',   N'maria.santos@kargabid.test',   N'AQAAAAIAAYagAAAAEO+aUQGkH6xnXQB/GJALHebVoBnYB5oczKTeKQGw1VUd7r/4ZzmOGP0ZQGq+7IBc2g==', N'Shipper', DATEADD(DAY, -30, @now)),
(@jose,  N'Jose Reyes',     N'jose.reyes@kargabid.test',     N'AQAAAAIAAYagAAAAEPI+Vn9gqJ1Ch0Kzakn/pcR08WW2gEcol3YWgWMMY7H3M3yOf6mSTrSglO6xmtXyCw==', N'Shipper', DATEADD(DAY, -28, @now)),
(@juan,  N'Juan Dela Cruz', N'juan.delacruz@kargabid.test',  N'AQAAAAIAAYagAAAAEOWaZxNImCA4KxADCytXpPEwT/rBfsU+ngGFu6n2v58TFiOD5T27BfdqYY9oYkur8A==', N'Driver',  DATEADD(DAY, -25, @now)),
(@pedro, N'Pedro Garcia',   N'pedro.garcia@kargabid.test',   N'AQAAAAIAAYagAAAAEKj0RULKPadoE7AGP+JpRraJ6X1juYUwcfQZvawOJgpZzHx0kY3a7OPamezGq58U7A==', N'Driver',  DATEADD(DAY, -24, @now)),
(@ana,   N'Ana Lopez',      N'ana.lopez@kargabid.test',      N'AQAAAAIAAYagAAAAEApI5sF7ipEECDrUN38EJZytXFyyTwtFudP1pioKxPLEuEA0hpg41mHBLqlqg4W52Q==', N'Driver',  DATEADD(DAY, -20, @now));

/* ---------- Jobs ---------- */
DECLARE @j1 UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000001'; -- Open, 2 pending bids  (Maria)
DECLARE @j2 UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000002'; -- Open, no bids         (Maria)
DECLARE @j3 UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000003'; -- Open, 1 pending bid   (Jose)
DECLARE @j4 UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000004'; -- InProgress, Pedro     (Maria)
DECLARE @j5 UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000005'; -- Completed, Juan       (Jose)
DECLARE @j6 UNIQUEIDENTIFIER = '22222222-0000-0000-0000-000000000006'; -- Open, 3 pending bids  (Jose)

INSERT INTO [dbo].[Jobs]
    ([Id], [ShipperId], [PickupLocation], [DropOffLocation], [CargoDescription], [TargetBudget], [Status], [CargoImageUrl], [CreatedAt], [AssignedDriverId])
VALUES
(@j1, @maria, N'Quezon City, Metro Manila', N'Batangas City, Batangas',  N'20 boxes of canned goods, approx. 400 kg',        9000.00,  N'Open',       N'https://picsum.photos/seed/kargabid-j1/600/400', DATEADD(HOUR, -6,  @now), NULL),
(@j2, @maria, N'Pasig City, Metro Manila',  N'Clark Freeport, Pampanga', N'Office furniture: 6 desks, 12 chairs',            12000.00, N'Open',       NULL,                                             DATEADD(HOUR, -2,  @now), NULL),
(@j3, @jose,  N'Makati City, Metro Manila', N'Tagaytay City, Cavite',    N'Catering equipment, fragile, approx. 150 kg',     5000.00,  N'Open',       N'https://picsum.photos/seed/kargabid-j3/600/400', DATEADD(HOUR, -12, @now), NULL),
(@j4, @maria, N'Valenzuela City',           N'Baguio City, Benguet',     N'Construction materials: 50 bags of cement',       15000.00, N'InProgress', NULL,                                             DATEADD(DAY,  -3,  @now), @pedro),
(@j5, @jose,  N'Taguig City, Metro Manila', N'Lipa City, Batangas',      N'2 refrigerators and 1 washing machine',           7000.00,  N'Completed',  N'https://picsum.photos/seed/kargabid-j5/600/400', DATEADD(DAY,  -10, @now), @juan),
(@j6, @jose,  N'Caloocan City',             N'San Fernando, Pampanga',   N'Rice sacks, 30 x 50 kg',                          8000.00,  N'Open',       NULL,                                             DATEADD(HOUR, -1,  @now), NULL);

/* ---------- Bids ---------- */
INSERT INTO [dbo].[Bids] ([Id], [JobId], [DriverId], [BidAmount], [Status], [CreatedAt]) VALUES
-- J1: open, Maria can accept either
('33333333-0000-0000-0000-000000000001', @j1, @juan,  8500.00,  N'Pending',  DATEADD(HOUR, -5, @now)),
('33333333-0000-0000-0000-000000000002', @j1, @pedro, 7900.00,  N'Pending',  DATEADD(HOUR, -4, @now)),
-- J3: open, one bid
('33333333-0000-0000-0000-000000000003', @j3, @ana,   4200.00,  N'Pending',  DATEADD(HOUR, -10, @now)),
-- J4: awarded to Pedro, others rejected
('33333333-0000-0000-0000-000000000004', @j4, @pedro, 13500.00, N'Accepted', DATEADD(DAY, -3, DATEADD(HOUR, 2, @now))),
('33333333-0000-0000-0000-000000000005', @j4, @juan,  14200.00, N'Rejected', DATEADD(DAY, -3, DATEADD(HOUR, 3, @now))),
('33333333-0000-0000-0000-000000000006', @j4, @ana,   14800.00, N'Rejected', DATEADD(DAY, -3, DATEADD(HOUR, 4, @now))),
-- J5: completed by Juan
('33333333-0000-0000-0000-000000000007', @j5, @juan,  6500.00,  N'Accepted', DATEADD(DAY, -10, DATEADD(HOUR, 1, @now))),
('33333333-0000-0000-0000-000000000008', @j5, @pedro, 6900.00,  N'Rejected', DATEADD(DAY, -10, DATEADD(HOUR, 2, @now))),
-- J6: open, three competing bids (good for testing accept -> auto-reject)
('33333333-0000-0000-0000-000000000009', @j6, @juan,  7600.00,  N'Pending',  DATEADD(MINUTE, -50, @now)),
('33333333-0000-0000-0000-000000000010', @j6, @pedro, 7200.00,  N'Pending',  DATEADD(MINUTE, -40, @now)),
('33333333-0000-0000-0000-000000000011', @j6, @ana,   7450.00,  N'Pending',  DATEADD(MINUTE, -30, @now));

COMMIT TRANSACTION;
GO

SELECT 'Users' AS [Table], COUNT(*) AS [Rows] FROM [dbo].[Users]
UNION ALL SELECT 'Jobs', COUNT(*) FROM [dbo].[Jobs]
UNION ALL SELECT 'Bids', COUNT(*) FROM [dbo].[Bids];
GO
