-- Optional SQL Server reporting views. Apply AFTER the API has created its tables.
-- This SQL is supplemental: EF Core's DbContext.EnsureCreated() creates the schema.
USE CrisisAid;
GO
CREATE OR ALTER VIEW dbo.vw_RequestStatusMetrics AS
SELECT CrisisId, Category, Status, COUNT_BIG(*) AS TotalRequests,
       SUM(CASE WHEN Urgency IN ('High','Critical') THEN 1 ELSE 0 END) AS HighUrgency
FROM dbo.Requests GROUP BY CrisisId, Category, Status;
GO
CREATE OR ALTER VIEW dbo.vw_ShelterOccupancy AS
SELECT Id, Name, Area, Capacity, Occupancy,
       Capacity - Occupancy AS SpaceAvailable,
       CAST(CASE WHEN Capacity > 0 THEN 100.0 * Occupancy / Capacity ELSE 0 END AS DECIMAL(6,2)) AS OccupancyPercent
FROM dbo.Shelters;
GO
CREATE OR ALTER VIEW dbo.vw_ResourceAvailability AS
SELECT Category, Area, SUM(AvailableQuantity) AS UnitsAvailable, COUNT_BIG(*) AS StockBatches
FROM dbo.Resources GROUP BY Category, Area;
GO
