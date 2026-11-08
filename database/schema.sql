/* ============================================================================
   KargaBid - SQL Server schema (reference / DBA review)
   Mirrors the model in src/KargaBid.Api/Data/KargaBidDbContext.cs.
   The source of truth is EF Core migrations:  dotnet ef database update
   ============================================================================ */

IF DB_ID(N'KargaBid') IS NULL
    CREATE DATABASE [KargaBid];
GO
USE [KargaBid];
GO

/* ---------- Users ---------- */
CREATE TABLE [dbo].[Users] (
    [Id]           UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Users_Id]        DEFAULT NEWSEQUENTIALID(),
    [FullName]     NVARCHAR(150)    NOT NULL,
    [Email]        NVARCHAR(256)    NOT NULL,
    [PasswordHash] NVARCHAR(512)    NOT NULL,
    [Role]         NVARCHAR(20)     NOT NULL,
    [CreatedAt]    DATETIME2        NOT NULL CONSTRAINT [DF_Users_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Users_Role] CHECK ([Role] IN (N'Shipper', N'Driver'))
);
CREATE UNIQUE INDEX [IX_Users_Email] ON [dbo].[Users] ([Email]);
GO

/* ---------- Jobs ---------- */
CREATE TABLE [dbo].[Jobs] (
    [Id]               UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Jobs_Id]        DEFAULT NEWSEQUENTIALID(),
    [ShipperId]        UNIQUEIDENTIFIER NOT NULL,
    [PickupLocation]   NVARCHAR(300)    NOT NULL,
    [DropOffLocation]  NVARCHAR(300)    NOT NULL,
    [CargoDescription] NVARCHAR(2000)   NOT NULL,
    [TargetBudget]     DECIMAL(18,2)    NOT NULL,
    [Status]           NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Jobs_Status]    DEFAULT N'Open',
    [CargoImageUrl]    NVARCHAR(2048)   NULL,
    [CreatedAt]        DATETIME2        NOT NULL CONSTRAINT [DF_Jobs_CreatedAt] DEFAULT SYSUTCDATETIME(),
    [AssignedDriverId] UNIQUEIDENTIFIER NULL,
    [RowVersion]       ROWVERSION       NOT NULL,
    CONSTRAINT [PK_Jobs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Jobs_Users_ShipperId]        FOREIGN KEY ([ShipperId])        REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Jobs_Users_AssignedDriverId] FOREIGN KEY ([AssignedDriverId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_Jobs_TargetBudget_Positive] CHECK ([TargetBudget] > 0),
    CONSTRAINT [CK_Jobs_Status] CHECK ([Status] IN (N'Open', N'InProgress', N'Completed'))
);
CREATE INDEX [IX_Jobs_Status_CreatedAt]  ON [dbo].[Jobs] ([Status], [CreatedAt]);
CREATE INDEX [IX_Jobs_ShipperId]         ON [dbo].[Jobs] ([ShipperId]);
CREATE INDEX [IX_Jobs_AssignedDriverId]  ON [dbo].[Jobs] ([AssignedDriverId]);
GO

/* ---------- Bids ---------- */
CREATE TABLE [dbo].[Bids] (
    [Id]        UNIQUEIDENTIFIER NOT NULL CONSTRAINT [DF_Bids_Id]        DEFAULT NEWSEQUENTIALID(),
    [JobId]     UNIQUEIDENTIFIER NOT NULL,
    [DriverId]  UNIQUEIDENTIFIER NOT NULL,
    [BidAmount] DECIMAL(18,2)    NOT NULL,
    [Status]    NVARCHAR(20)     NOT NULL CONSTRAINT [DF_Bids_Status]    DEFAULT N'Pending',
    [CreatedAt] DATETIME2        NOT NULL CONSTRAINT [DF_Bids_CreatedAt] DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_Bids] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Bids_Jobs_JobId]     FOREIGN KEY ([JobId])    REFERENCES [dbo].[Jobs]  ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Bids_Users_DriverId] FOREIGN KEY ([DriverId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [CK_Bids_BidAmount_Positive] CHECK ([BidAmount] > 0),
    CONSTRAINT [CK_Bids_Status] CHECK ([Status] IN (N'Pending', N'Accepted', N'Rejected'))
);
-- One bid per driver per job
CREATE UNIQUE INDEX [IX_Bids_JobId_DriverId] ON [dbo].[Bids] ([JobId], [DriverId]);
-- At most one accepted bid per job
CREATE UNIQUE INDEX [UX_Bids_JobId_Accepted] ON [dbo].[Bids] ([JobId]) WHERE [Status] = N'Accepted';
CREATE INDEX [IX_Bids_DriverId] ON [dbo].[Bids] ([DriverId]);
GO
